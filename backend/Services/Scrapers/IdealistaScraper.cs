using EstateAggregator.DTOs;
using EstateAggregator.Models;

namespace EstateAggregator.Services.Scrapers;

// Idealista does not offer a self-serve public API, and no API credentials are
// currently available. This scraper is intentionally stubbed rather than
// scraping the live site. The HttpClient/Polly plumbing below is wired so a
// real implementation (HTML scraping via HtmlAgilityPack, or a future
// official API integration) can be dropped in here without touching the
// surrounding orchestration.
public class IdealistaScraper : IPropertyScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IdealistaScraper> _logger;

    public string Source => "Idealista";

    public IdealistaScraper(HttpClient httpClient, ILogger<IdealistaScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<ScraperReportDto> ScrapeAsync(AppSetting settings, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("{Source} scraping not yet implemented — no API credentials / self-serve API available", Source);

        // TODO: implement once a real integration approach is decided
        // (HTML scraping with HtmlAgilityPack, or an official partner API).
        return Task.FromResult(new ScraperReportDto
        {
            Source = Source,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow,
            HasErrors = true,
            Errors = { "Integration not yet configured" }
        });
    }
}
