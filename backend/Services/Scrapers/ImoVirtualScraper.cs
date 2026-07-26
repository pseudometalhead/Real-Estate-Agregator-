using EstateAggregator.DTOs;
using EstateAggregator.Models;

namespace EstateAggregator.Services.Scrapers;

// See IdealistaScraper.cs for the rationale: stubbed until a real
// integration approach (API or HTML scraping) is decided.
public class ImoVirtualScraper : IPropertyScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImoVirtualScraper> _logger;

    public string Source => "ImoVirtual";

    public ImoVirtualScraper(HttpClient httpClient, ILogger<ImoVirtualScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<ScraperReportDto> ScrapeAsync(AppSetting settings, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("{Source} scraping not yet implemented — no API credentials / self-serve API available", Source);

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
