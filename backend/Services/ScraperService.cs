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
    private readonly ILogger<ScraperService> _logger;

    public ScraperService(IEnumerable<IPropertyScraper> scrapers, EstateDbContext db, ILogger<ScraperService> logger)
    {
        _scrapers = scrapers;
        _db = db;
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
                PropertiesSkipped = scraperReport.PropertiesSkipped,
                HasErrors = scraperReport.HasErrors,
                ErrorsJson = JsonSerializer.Serialize(scraperReport.Errors),
                DurationSeconds = (int)(runEnd - runStart).TotalSeconds
            });

            aggregate.Merge(scraperReport);
        }

        settings.LastScrapedAt = DateTime.UtcNow;
        aggregate.EndTime = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Scraper run completed: {Found} found, {Added} added",
            aggregate.PropertiesFound, aggregate.PropertiesAdded);

        return aggregate;
    }

    private static bool IsScraperEnabled(string source, AppSetting settings) => source switch
    {
        "Idealista" => settings.ScrapeIdealistaEnabled,
        "ImoVirtual" => settings.ScrapeImoVirtualEnabled,
        "Imobiliario" => settings.ScrapeImobiliarioEnabled,
        "CasaSapo" => settings.ScrapeCasaSapoEnabled,
        _ => false
    };
}
