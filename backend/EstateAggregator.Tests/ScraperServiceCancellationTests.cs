using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using EstateAggregator.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EstateAggregator.Tests;

// Live symptom this pins: run-now's real-world duration (~11 minutes across
// all 6 sources) exceeded the frontend's old 6-minute axios timeout, so the
// browser aborted the request mid-scrape. RunScrapersAsync's foreach loop
// didn't check for cancellation between sources, so every scraper queued
// AFTER the abort immediately threw on its first cancellable await and got
// logged as its own "Scraper X failed" — a cascade of failures for sources
// that never actually ran, and the endpoint returned a 500 even though
// nothing was actually broken. These tests pin the fix: the loop now stops
// cleanly once cancellation is detected, and the final bookkeeping save
// isn't lost just because the caller's token was already cancelled.
public class ScraperServiceCancellationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly EstateDbContext _db;

    public ScraperServiceCancellationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<EstateDbContext>().UseSqlite(_connection).Options;
        _db = new EstateDbContext(options);
        _db.Database.EnsureCreated();
        _db.AppSettings.Add(new AppSetting());
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // A scraper that cancels the shared token partway through its own work —
    // simulating the client disconnecting while THIS source is still running.
    private class SelfCancellingScraper : IPropertyScraper
    {
        private readonly CancellationTokenSource _cts;
        public string Source => "Idealista";
        public SelfCancellingScraper(CancellationTokenSource cts) => _cts = cts;

        public Task<ScraperReportDto> ScrapeAsync(AppSetting settings, CancellationToken cancellationToken = default)
        {
            _cts.Cancel();
            return Task.FromResult(new ScraperReportDto { Source = Source, PropertiesFound = 5, PropertiesAdded = 5 });
        }
    }

    private class NeverCalledScraper : IPropertyScraper
    {
        public string Source { get; }
        public bool WasCalled { get; private set; }
        public NeverCalledScraper(string source) => Source = source;

        public Task<ScraperReportDto> ScrapeAsync(AppSetting settings, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new ScraperReportDto { Source = Source });
        }
    }

    [Fact]
    public async Task CancellationMidRun_StopsBeforeLaterScrapers_InsteadOfCascadingFailures()
    {
        var cts = new CancellationTokenSource();
        var first = new SelfCancellingScraper(cts);
        var second = new NeverCalledScraper("ImoVirtual");
        var third = new NeverCalledScraper("CustoJusto");

        var service = new ScraperService(
            new IPropertyScraper[] { first, second, third },
            _db,
            new GeocodingService(new HttpClient(), NullLogger<GeocodingService>.Instance),
            NullLogger<ScraperService>.Instance);

        var report = await service.RunScrapersAsync(cts.Token);

        // The first (cancelling) scraper still ran and its result is
        // reflected — only the ones queued AFTER cancellation are skipped.
        Assert.Equal(5, report.PropertiesAdded);
        Assert.False(second.WasCalled);
        Assert.False(third.WasCalled);
    }

    [Fact]
    public async Task CancellationMidRun_StillPersistsScraperRunBookkeeping()
    {
        var cts = new CancellationTokenSource();
        var first = new SelfCancellingScraper(cts);

        var service = new ScraperService(
            new IPropertyScraper[] { first },
            _db,
            new GeocodingService(new HttpClient(), NullLogger<GeocodingService>.Instance),
            NullLogger<ScraperService>.Instance);

        await service.RunScrapersAsync(cts.Token);

        // The final SaveChangesAsync must not be skipped just because the
        // token was already cancelled by the time the loop finished.
        var runs = await _db.ScraperRuns.ToListAsync(CancellationToken.None);
        Assert.Single(runs);
        Assert.Equal("Idealista", runs[0].Source);

        var settings = await _db.AppSettings.FirstAsync(CancellationToken.None);
        Assert.NotNull(settings.LastScrapedAt);
    }
}
