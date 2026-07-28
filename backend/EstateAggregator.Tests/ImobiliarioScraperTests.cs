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

// custojusto.pt's search-results page only ever carries a single cover
// photo (ImageFullURL) — verified live that a real listing's own detail
// page had 13 photos in adData.extraImages versus that same 1. The
// per-listing detail page was already fetched conditionally (for a full
// description / exact lat-lon), so pulling the gallery from that same
// response is free once "needs detail" also triggers for listings that
// still only have their one guaranteed photo.
public class ImobiliarioScraperTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly EstateDbContext _db;

    public ImobiliarioScraperTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<EstateDbContext>().UseSqlite(_connection).Options;
        _db = new EstateDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private const string LongDescription =
        "Apartamento T2 completamente renovado, com excelente localização, próximo de todos os serviços, " +
        "transportes públicos e comércio local. Cozinha totalmente equipada, casas de banho remodeladas, " +
        "aquecimento central e vistas desafogadas sobre o rio. Estacionamento incluído no preço final total.";

    private static string SearchPageHtml(string listId, string relativeUrl) => $$"""
        <script id="__NEXT_DATA__">
        {
          "props": {
            "pageProps": {
              "listItems": [
                {
                  "listID": "{{listId}}",
                  "title": "Apartamento T2",
                  "body": "Apartamento T2 curto",
                  "name": "Agência Exemplo",
                  "type": "sell",
                  "price": 200000,
                  "url": "{{relativeUrl}}",
                  "imageFullURL": "https://prod-images.custojusto.pt/gallery/search-cover-only.jpg",
                  "locationNames": { "district": "Porto", "county": "Porto", "parish": "Bonfim" },
                  "params": { "rooms": "T2", "size": "80m2" }
                }
              ]
            }
          }
        }
        </script>
        """;

    private static string DetailPageHtml() => $$"""
        <script id="__NEXT_DATA__">
        {
          "props": {
            "pageProps": {
              "adData": {
                "body": "{{LongDescription}}",
                "location": { "lat": 41.15, "lon": -8.6 },
                "params": { "energyrating": { "name": "B" } },
                "extraImages": [
                  { "yams_oid": "https://prod-images.custojusto.pt/{rule}/photo-1.jpg", "deleted": true },
                  { "yams_oid": "https://prod-images.custojusto.pt/{rule}/photo-2.jpg", "deleted": true },
                  { "yams_oid": "https://prod-images.custojusto.pt/{rule}/photo-3.jpg", "deleted": true }
                ]
              }
            }
          }
        }
        </script>
        """;

    private class RoutingHandler : HttpMessageHandler
    {
        private readonly string _searchHtml;
        private readonly string? _detailHtml;
        public int DetailRequestCount { get; private set; }

        public RoutingHandler(string searchHtml, string? detailHtml)
        {
            _searchHtml = searchHtml;
            _detailHtml = detailHtml;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            // The search page's own path is exactly ".../imobiliario/apartamentos"
            // (optionally with a "?o=page" query string); a detail page has an
            // extra listing-slug segment appended after "apartamentos/".
            if (path.TrimEnd('/').EndsWith("/imobiliario/apartamentos"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_searchHtml) });
            }

            DetailRequestCount++;
            var body = _detailHtml ?? "";
            var status = _detailHtml == null ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
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
    public async Task ScrapeAsync_NewListing_FetchesDetailAndUsesFullGallery()
    {
        var relativeUrl = "/porto/imobiliario/apartamentos/t2-bonfim-12345";
        var handler = new RoutingHandler(SearchPageHtml("12345", relativeUrl), DetailPageHtml());
        var httpClient = new HttpClient(handler);
        var scraper = new ImobiliarioScraper(httpClient, _db, new DeduplicationService(), NullLogger<ImobiliarioScraper>.Instance);

        await scraper.ScrapeAsync(MakeSettings());

        Assert.Equal(1, handler.DetailRequestCount);
        var saved = await _db.Properties.FirstAsync();
        var photos = JsonSerializer.Deserialize<string[]>(saved.PhotosJson)!;
        Assert.Equal(3, photos.Length);
        Assert.DoesNotContain("https://prod-images.custojusto.pt/gallery/search-cover-only.jpg", photos);
    }

    [Fact]
    public async Task ScrapeAsync_ExistingListingWithOnlyCoverPhoto_ReFetchesToBackfillGallery()
    {
        var relativeUrl = "/porto/imobiliario/apartamentos/t2-bonfim-12346";
        _db.Properties.Add(new Property
        {
            Url = $"https://www.custojusto.pt{relativeUrl}",
            Source = "CustoJusto",
            LocationString = "Porto, Bonfim",
            Description = LongDescription,
            Lat = 41.15,
            Lng = -8.6,
            PhotosJson = JsonSerializer.Serialize(new[] { "https://prod-images.custojusto.pt/gallery/search-cover-only.jpg" }),
            DedupHash = "existing-hash",
        });
        await _db.SaveChangesAsync();

        var handler = new RoutingHandler(SearchPageHtml("12346", relativeUrl), DetailPageHtml());
        var httpClient = new HttpClient(handler);
        var scraper = new ImobiliarioScraper(httpClient, _db, new DeduplicationService(), NullLogger<ImobiliarioScraper>.Instance);

        await scraper.ScrapeAsync(MakeSettings());

        Assert.Equal(1, handler.DetailRequestCount);
        var saved = await _db.Properties.FirstAsync();
        var photos = JsonSerializer.Deserialize<string[]>(saved.PhotosJson)!;
        Assert.Equal(3, photos.Length);
    }

    [Fact]
    public async Task ScrapeAsync_DetailFetchFails_KeepsSearchCoverPhoto()
    {
        var relativeUrl = "/porto/imobiliario/apartamentos/t2-bonfim-12347";
        var handler = new RoutingHandler(SearchPageHtml("12347", relativeUrl), detailHtml: null);
        var httpClient = new HttpClient(handler);
        var scraper = new ImobiliarioScraper(httpClient, _db, new DeduplicationService(), NullLogger<ImobiliarioScraper>.Instance);

        await scraper.ScrapeAsync(MakeSettings());

        var saved = await _db.Properties.FirstAsync();
        var photos = JsonSerializer.Deserialize<string[]>(saved.PhotosJson)!;
        Assert.Single(photos);
        Assert.Equal("https://prod-images.custojusto.pt/gallery/search-cover-only.jpg", photos[0]);
    }
}
