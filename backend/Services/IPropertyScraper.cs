using EstateAggregator.DTOs;
using EstateAggregator.Models;

namespace EstateAggregator.Services;

public interface IPropertyScraper
{
    string Source { get; }
    Task<ScraperReportDto> ScrapeAsync(AppSetting settings, CancellationToken cancellationToken = default);
}
