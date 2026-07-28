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

// casa.sapo.pt lazy-loads photos below the first few cards on a search
// page — verified live that only the first 4 of 26 listings had a real
// "srcset" on their <source> element; the rest carry "data-srcset" instead
// (the real "srcset" is populated by JS on scroll) with the <img src> a 1x1
// base64 placeholder GIF and the real URL sitting in "data-src". The
// scraper only ever checked "srcset", so ~85% of real listings silently got
// an empty PhotosJson despite having a real photo in the page's own HTML —
// this is why so many CasaSapo cards showed the 🏠 fallback in the UI.
public class CasaSapoScraperTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly EstateDbContext _db;

    public CasaSapoScraperTests()
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

    private static string CardHtml(string id, string photoMarkup) =>
        $"<div class='property-info-content'>" +
        $"<a class='property-info' href='https://casa.sapo.pt/imovel-{id}.html'></a>" +
        $"<div class='property-type'>Apartamento T2</div>" +
        $"<div class='property-location'>Porto, Bonfim</div>" +
        $"<div class='property-features-text'>75 m2</div>" +
        $"<div class='property-price-value'>200.000 &euro;</div>" +
        $"</div>" +
        $"<div class='property-description'>Description text</div>" +
        $"<div class='property-media'>{photoMarkup}</div>";

    private const string EagerPhoto =
        "<picture><source srcset='https://media.casasapo.pt/eager-photo.jpg.webp 1x' type='image/webp' /></picture>";

    private const string LazyPhoto =
        "<picture><source data-srcset='https://media.casasapo.pt/lazy-photo.jpg.webp 1x' type='image/webp' />" +
        "<img src='data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7' data-src='https://media.casasapo.pt/lazy-photo-fallback.jpg' /></picture>";

    private class SinglePageHandler : HttpMessageHandler
    {
        private readonly string _body;
        public SinglePageHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_body) });
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
    public async Task ScrapeAsync_LazyLoadedPhoto_IsCapturedViaDataSrcset()
    {
        var html = CardHtml("lazy-1", LazyPhoto);
        var httpClient = new HttpClient(new SinglePageHandler(html));
        var scraper = new CasaSapoScraper(httpClient, _db, new DeduplicationService(), NullLogger<CasaSapoScraper>.Instance);

        await scraper.ScrapeAsync(MakeSettings());

        var saved = await _db.Properties.FirstAsync();
        var photos = JsonSerializer.Deserialize<string[]>(saved.PhotosJson);
        Assert.Contains("https://media.casasapo.pt/lazy-photo.jpg.webp", photos);
    }

    [Fact]
    public async Task ScrapeAsync_EagerLoadedPhoto_StillWorksAsBefore()
    {
        var html = CardHtml("eager-1", EagerPhoto);
        var httpClient = new HttpClient(new SinglePageHandler(html));
        var scraper = new CasaSapoScraper(httpClient, _db, new DeduplicationService(), NullLogger<CasaSapoScraper>.Instance);

        await scraper.ScrapeAsync(MakeSettings());

        var saved = await _db.Properties.FirstAsync();
        var photos = JsonSerializer.Deserialize<string[]>(saved.PhotosJson);
        Assert.Contains("https://media.casasapo.pt/eager-photo.jpg.webp", photos);
    }

    // Each listing's media block is a swiper carousel with one photo per
    // ".swiper-slide" — verified live: a real search-results page had 4-5
    // distinct photos per listing this way, all free (already in the one
    // page fetch this scraper makes). The scraper used to only ever look at
    // the first <source> found anywhere in the whole media div, so every
    // listing got capped at 1 photo even when several were already on the
    // page.
    [Fact]
    public async Task ScrapeAsync_MultipleSwiperSlides_CapturesEveryPhoto()
    {
        var photoMarkup =
            "<div class='property-media-swiper swiper-container'><div class='swiper-wrapper'>" +
            "<div class='swiper-slide'><picture><source srcset='https://media.casasapo.pt/slide-1.jpg.webp 1x' /></picture></div>" +
            "<div class='swiper-slide'><picture><source data-srcset='https://media.casasapo.pt/slide-2.jpg.webp 1x' /></picture></div>" +
            "<div class='swiper-slide'><picture><source data-srcset='https://media.casasapo.pt/slide-3.jpg.webp 1x' /></picture></div>" +
            "</div></div>";
        var html = CardHtml("multi-1", photoMarkup);
        var httpClient = new HttpClient(new SinglePageHandler(html));
        var scraper = new CasaSapoScraper(httpClient, _db, new DeduplicationService(), NullLogger<CasaSapoScraper>.Instance);

        await scraper.ScrapeAsync(MakeSettings());

        var saved = await _db.Properties.FirstAsync();
        var photos = JsonSerializer.Deserialize<string[]>(saved.PhotosJson)!;
        Assert.Equal(3, photos.Length);
        Assert.Contains("https://media.casasapo.pt/slide-1.jpg.webp", photos);
        Assert.Contains("https://media.casasapo.pt/slide-2.jpg.webp", photos);
        Assert.Contains("https://media.casasapo.pt/slide-3.jpg.webp", photos);
    }

    [Fact]
    public async Task ScrapeAsync_NoSrcsetAtAll_FallsBackToImgDataSrc()
    {
        var photoMarkup = "<picture><img src='data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7' " +
            "data-src='https://media.casasapo.pt/img-data-src-only.jpg' /></picture>";
        var html = CardHtml("imgonly-1", photoMarkup);
        var httpClient = new HttpClient(new SinglePageHandler(html));
        var scraper = new CasaSapoScraper(httpClient, _db, new DeduplicationService(), NullLogger<CasaSapoScraper>.Instance);

        await scraper.ScrapeAsync(MakeSettings());

        var saved = await _db.Properties.FirstAsync();
        var photos = JsonSerializer.Deserialize<string[]>(saved.PhotosJson);
        Assert.Contains("https://media.casasapo.pt/img-data-src-only.jpg", photos);
    }
}
