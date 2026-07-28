using System.Net;
using EstateAggregator.Data;
using EstateAggregator.Models;
using EstateAggregator.Services;
using EstateAggregator.Services.Scrapers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EstateAggregator.Tests;

// caixaimobiliario.pt has no server-side district filter (see the
// scraper's own class comment) — every page is nationwide results ordered
// by date, with Porto-matching narrowed down in code after the fact.
// Verified live: a real "newest first" page 1 had listings from Setúbal,
// Beja, Aveiro, Madeira, Coimbra, Lisboa, Castelo Branco, and Portalegre —
// zero from Porto, purely by chance of what had recently been listed
// nationwide. The scraper used to stop pagination the moment a page had
// zero PORTO MATCHES, which isn't the same thing as "no more listings
// exist" — so a genuinely unlucky page 1 silently ended the whole run
// with a "0 found" report despite Porto listings sitting on page 2. This
// test pins the fix: pagination only stops once a page has zero listings
// at all, not zero matching ones.
public class CaixaImobiliarioScraperTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly EstateDbContext _db;

    public CaixaImobiliarioScraperTests()
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

    private static string CardHtml(string district, string municipality, string price, string id) =>
        $"<div class='result_imovel_txt'>" +
        $"<a href='/comprar/imovel.jsp?id={id}'>Apartamento T1</a><br/>" +
        $"<b>{price} &euro;</b><br/>" +
        $"{district} | {municipality}<br/>" +
        $"<span style='font-size:11px;'>54 m&sup2; de area bruta</span>" +
        $"<span>Description text here</span>" +
        $"<span>Ref. 001/{id}</span>" +
        $"</div>";

    private class SequencedPagesHandler : HttpMessageHandler
    {
        private readonly Queue<string> _pages;
        public int RequestCount { get; private set; }
        public List<string> RequestedUrls { get; } = new();

        public SequencedPagesHandler(IEnumerable<string> pages) => _pages = new Queue<string>(pages);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestedUrls.Add(request.RequestUri!.ToString());
            var body = _pages.Count > 0 ? _pages.Dequeue() : string.Empty;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }

    private static AppSetting MakeSettings() => new()
    {
        DistrictsJson = "[\"Porto\"]",
        PriceMin = 50000,
        PriceMax = 300000,
        MaxPagesPerSource = 5,
    };

    [Fact]
    public async Task ScrapeAsync_PageWithNoPortoMatches_StillChecksLaterPages()
    {
        var page1 = string.Concat(
            CardHtml("Setúbal", "Moita", "95.000", "1"),
            CardHtml("Beja", "Alvito", "225.000", "2"));
        var page2 = CardHtml("Porto", "Vila Nova de Gaia", "180.000", "3");
        var page3 = ""; // no more listings — true end of results

        var handler = new SequencedPagesHandler(new[] { page1, page2, page3 });
        var httpClient = new HttpClient(handler);
        var scraper = new CaixaImobiliarioScraper(
            httpClient, _db, new DeduplicationService(), NullLogger<CaixaImobiliarioScraper>.Instance);

        var report = await scraper.ScrapeAsync(MakeSettings());

        Assert.Equal(1, report.PropertiesFound);
        Assert.Equal(1, report.PropertiesAdded);
        Assert.Equal(3, handler.RequestCount); // paged through page 1 (no match), page 2 (match), page 3 (empty -> stop)
    }

    // Pins the actual root cause, not just the symptom above: the site's
    // real pagination is JS-driven (a "Seguinte" link calling
    // changePAGE(total, pageSize, targetPage, offset, ...) against a form),
    // not a plain GET with just "pgnr" — verified live that "pgnr" alone is
    // silently ignored, and every page returned byte-identical results
    // without "ofs" (pageSize * (page-1)) also present in the query string.
    [Fact]
    public async Task ScrapeAsync_RequestsIncludeCorrectOffsetPerPage()
    {
        var page1 = CardHtml("Lisboa", "Loures", "95.000", "1");
        var page2 = CardHtml("Porto", "Felgueiras", "180.000", "2");
        var page3 = "";

        var handler = new SequencedPagesHandler(new[] { page1, page2, page3 });
        var httpClient = new HttpClient(handler);
        var scraper = new CaixaImobiliarioScraper(
            httpClient, _db, new DeduplicationService(), NullLogger<CaixaImobiliarioScraper>.Instance);

        await scraper.ScrapeAsync(MakeSettings());

        Assert.Equal(3, handler.RequestedUrls.Count);
        Assert.Contains("pgnr=1", handler.RequestedUrls[0]);
        Assert.Contains("ofs=0", handler.RequestedUrls[0]);
        Assert.Contains("pgnr=2", handler.RequestedUrls[1]);
        Assert.Contains("ofs=8", handler.RequestedUrls[1]);
        Assert.Contains("pgnr=3", handler.RequestedUrls[2]);
        Assert.Contains("ofs=16", handler.RequestedUrls[2]);
    }

    [Fact]
    public async Task ScrapeAsync_FirstPageEmpty_StopsImmediately()
    {
        var handler = new SequencedPagesHandler(new[] { "" });
        var httpClient = new HttpClient(handler);
        var scraper = new CaixaImobiliarioScraper(
            httpClient, _db, new DeduplicationService(), NullLogger<CaixaImobiliarioScraper>.Instance);

        var report = await scraper.ScrapeAsync(MakeSettings());

        Assert.Equal(0, report.PropertiesFound);
        Assert.Equal(1, handler.RequestCount);
    }
}
