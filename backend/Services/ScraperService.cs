using System.Text.Json;
using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Services;

public class ScraperService
{
    private readonly IEnumerable<IPropertyScraper> _scrapers;
    private readonly EstateDbContext _db;
    private readonly GeocodingService _geocodingService;
    private readonly ILogger<ScraperService> _logger;

    public ScraperService(
        IEnumerable<IPropertyScraper> scrapers, EstateDbContext db, GeocodingService geocodingService,
        ILogger<ScraperService> logger)
    {
        _scrapers = scrapers;
        _db = db;
        _geocodingService = geocodingService;
        _logger = logger;
    }

    public async Task<ScraperReportDto> RunScrapersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting scraper run at {Time}", DateTime.UtcNow);

        var settings = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken);
        var aggregate = new ScraperReportDto { Source = "All", StartTime = DateTime.UtcNow };

        if (settings == null)
        {
            _logger.LogWarning("No AppSettings row found — skipping scraper run");
            aggregate.HasErrors = true;
            aggregate.Errors.Add("AppSettings not configured");
            aggregate.EndTime = DateTime.UtcNow;
            return aggregate;
        }

        foreach (var scraper in _scrapers)
        {
            if (!IsScraperEnabled(scraper.Source, settings))
                continue;

            // Once the caller's token is cancelled (e.g. the HTTP client
            // gave up waiting), every subsequent scraper would otherwise
            // throw immediately on its first cancellable await, logged as
            // its own "Scraper X failed" — a cascade of failures for
            // sources that never actually ran, not a real per-source
            // problem. Stop the loop cleanly instead and report what
            // completed before the cancellation.
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Scraper run cancelled before {Source} started", scraper.Source);
                break;
            }

            var runStart = DateTime.UtcNow;
            ScraperReportDto scraperReport;

            try
            {
                scraperReport = await scraper.ScrapeAsync(settings, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scraper {Source} failed", scraper.Source);
                scraperReport = new ScraperReportDto
                {
                    Source = scraper.Source,
                    StartTime = runStart,
                    EndTime = DateTime.UtcNow,
                    HasErrors = true,
                    Errors = { ex.Message }
                };
            }

            var runEnd = scraperReport.EndTime ?? DateTime.UtcNow;

            _db.ScraperRuns.Add(new ScraperRun
            {
                Source = scraper.Source,
                RunStartTime = runStart,
                RunEndTime = runEnd,
                PropertiesFound = scraperReport.PropertiesFound,
                PropertiesAdded = scraperReport.PropertiesAdded,
                PropertiesUpdated = scraperReport.PropertiesUpdated,
                PropertiesLinked = scraperReport.PropertiesLinked,
                PropertiesSkipped = scraperReport.PropertiesSkipped,
                HasErrors = scraperReport.HasErrors,
                ErrorsJson = JsonSerializer.Serialize(scraperReport.Errors),
                DurationSeconds = (int)(runEnd - runStart).TotalSeconds
            });

            aggregate.Merge(scraperReport);
        }

        settings.LastScrapedAt = DateTime.UtcNow;
        aggregate.EndTime = DateTime.UtcNow;

        // Deliberately CancellationToken.None: if the loop above broke early
        // because the caller's token was already cancelled, using that same
        // token here would throw before this save ever ran — losing the
        // ScraperRun audit rows (and LastScrapedAt) for every source that
        // DID complete before the cancellation, even though their actual
        // scraped Properties are already safely committed (each scraper
        // saves incrementally as it goes). This is bookkeeping, not new
        // scraping work, so it's worth finishing regardless.
        await _db.SaveChangesAsync(CancellationToken.None);

        try
        {
            await _geocodingService.GeocodeMissingAsync(_db, cancellationToken);
        }
        catch (Exception ex)
        {
            // Geocoding is a nice-to-have for the map view — a failure here
            // shouldn't mark the whole scrape run as failed.
            _logger.LogWarning(ex, "Geocoding pass failed");
        }

        // AI fact-extraction is NOT run automatically here — the user
        // doesn't want a paid Anthropic API balance, so AiEnrichmentService
        // (which calls that API directly) is only reachable via the manual
        // POST /api/scrapers/enrich-now endpoint, for if that ever changes.
        // The working, no-cost path is GET pending-enrichment / POST
        // apply-enrichment on ScrapersController — Claude (this coding
        // assistant, already running under the user's existing session, no
        // extra billing) reads the batch, extracts the facts itself, and
        // posts the results back. See those endpoints' comments.

        _logger.LogInformation("Scraper run completed: {Found} found, {Added} added",
            aggregate.PropertiesFound, aggregate.PropertiesAdded);

        return aggregate;
    }

    private static bool IsScraperEnabled(string source, AppSetting settings) => source switch
    {
        "Idealista" => settings.ScrapeIdealistaEnabled,
        "ImoVirtual" => settings.ScrapeImoVirtualEnabled,
        "CustoJusto" => settings.ScrapeImobiliarioEnabled,
        "CasaSapo" => settings.ScrapeCasaSapoEnabled,
        "CaixaImobiliario" => settings.ScrapeCaixaImobiliarioEnabled,
        "Santander" => settings.ScrapeSantanderEnabled,
        _ => false
    };
}
