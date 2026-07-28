using System.Net;
using System.Text.Json;
using EstateAggregator.Data;
using EstateAggregator.Models;
using EstateAggregator.Services;
using EstateAggregator.Services.Scrapers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EstateAggregator.Tests;

// The search endpoint's own "multimedia.images" is truncated to a single
// entry no matter how many photos a listing actually has — verified live
// that a real search result with numPhotos=13 still only carried 1 image
// in multimedia.images. GET /v1/property/{ad_id} (the per-listing detail
// endpoint) is the only way to get the full gallery. These tests pin that
// the scraper actually calls it and uses the fuller result.
public class IdealistaScraperTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly EstateDbContext _db;
    private const string ApiKeyEnvVar = "IDEALISTA_RAPIDAPI_KEY";

    public IdealistaScraperTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<EstateDbContext>().UseSqlite(_connection).Options;
        _db = new EstateDbContext(options);
        _db.Database.EnsureCreated();
        Environment.SetEnvironmentVariable(ApiKeyEnvVar, "test-key");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ApiKeyEnvVar, null);
        _db.Dispose();
        _connection.Dispose();
    }

    private const string AutocompleteResponse =
        """{"locations":[{"locationId":"0-EU-PT-13","name":"Porto","subTypeText":"Distrito"}]}""";

    private static string SearchResponse(string propertyCode) => $$"""
        {
          "total": 1,
          "totalPages": 1,
          "actualPage": 1,
          "elementList": [
            {
              "propertyCode": "{{propertyCode}}",
              "price": 200000,
              "thumbnail": "https://img4.idealista.pt/search-thumbnail-only.jpg",
              "size": 75,
              "rooms": 2,
              "province": "Porto",
              "municipality": "Porto",
              "url": "https://www.idealista.pt/imovel/{{propertyCode}}/",
              "description": "Apartamento T2"
            }
          ]
        }
        """;

    private const string DetailResponseWithFullGallery = """
        {
          "multimedia": {
            "images": [
              { "url": "https://img4.idealista.pt/full-1.jpg" },
              { "url": "https://img4.idealista.pt/full-2.jpg" },
              { "url": "https://img4.idealista.pt/full-3.jpg" }
            ]
          }
        }
        """;

    private class RoutingHandler : HttpMessageHandler
    {
        private readonly string _propertyCode;
        private readonly string? _detailResponse;
        public int DetailRequestCount { get; private set; }

        public RoutingHandler(string propertyCode, string? detailResponse)
        {
            _propertyCode = propertyCode;
            _detailResponse = detailResponse;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            string body;
            var status = HttpStatusCode.OK;

            if (path.Contains("/locations/autocomplete"))
            {
                body = AutocompleteResponse;
            }
            else if (path.Contains($"/v1/property/{_propertyCode}"))
            {
                DetailRequestCount++;
                if (_detailResponse == null)
                {
                    status = HttpStatusCode.InternalServerError;
                    body = "";
                }
                else
                {
                    body = _detailResponse;
                }
            }
            else if (path.Contains("/v1/search"))
            {
                body = SearchResponse(_propertyCode);
            }
            else
            {
                status = HttpStatusCode.NotFound;
                body = "";
            }

            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static AppSetting MakeSettings() => new()
    {
        DistrictsJson = JsonSerializer.Serialize(new[] { "Porto" }),
        PriceMin = 50000,
        PriceMax = 300000,
        RoomsMin = 1,
        RoomsMax = 4,
        MaxPagesPerSource = 1,
    };

    [Fact]
    public async Task ScrapeAsync_CallsDetailEndpoint_AndUsesFullGalleryOverSearchThumbnail()
    {
        var handler = new RoutingHandler("35206503", DetailResponseWithFullGallery);
        var httpClient = new HttpClient(handler);
        var scraper = new IdealistaScraper(httpClient, _db, new DeduplicationService(), NullLogger<IdealistaScraper>.Instance);

        await scraper.ScrapeAsync(MakeSettings());

        Assert.Equal(1, handler.DetailRequestCount);
        var saved = await _db.Properties.FirstAsync();
        var photos = JsonSerializer.Deserialize<string[]>(saved.PhotosJson)!;
        Assert.Equal(3, photos.Length);
        Assert.DoesNotContain("https://img4.idealista.pt/search-thumbnail-only.jpg", photos);
    }

    [Fact]
    public async Task ScrapeAsync_DetailEndpointFails_FallsBackToSearchThumbnail()
    {
        var handler = new RoutingHandler("35206504", detailResponse: null); // 500 on detail
        var httpClient = new HttpClient(handler);
        var scraper = new IdealistaScraper(httpClient, _db, new DeduplicationService(), NullLogger<IdealistaScraper>.Instance);

        await scraper.ScrapeAsync(MakeSettings());

        var saved = await _db.Properties.FirstAsync();
        var photos = JsonSerializer.Deserialize<string[]>(saved.PhotosJson)!;
        Assert.Single(photos);
        Assert.Equal("https://img4.idealista.pt/search-thumbnail-only.jpg", photos[0]);
    }
}
