using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Services;

public class ReportService
{
    private readonly EstateDbContext _db;

    // Same 6 sources ScraperService.IsScraperEnabled knows about — kept here
    // as its own list (not shared) so every platform always appears in the
    // dashboard, even ones that have never run, rather than only showing up
    // once a ScraperRun row exists for them.
    private static readonly string[] KnownSources =
        { "Idealista", "ImoVirtual", "CustoJusto", "CasaSapo", "CaixaImobiliario", "Santander" };

    public ReportService(EstateDbContext db)
    {
        _db = db;
    }

    public async Task<DailyReportDto> GetDailyReportAsync()
    {
        var yesterday = DateTime.UtcNow.AddDays(-1);

        var report = new DailyReportDto
        {
            GeneratedAt = DateTime.UtcNow,

            ContactsMadeLast24h = await _db.CommHistoryEntries
                .CountAsync(c => c.CreatedAt > yesterday && c.Direction == "Outbound"),

            RecentContacts = await _db.CommHistoryEntries
                .Where(c => c.CreatedAt > yesterday && c.Direction == "Outbound")
                .Include(c => c.MyListing).ThenInclude(m => m!.Property)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new ContactSummaryDto
                {
                    Channel = c.Channel,
                    CreatedAt = c.CreatedAt,
                    PropertyLocation = c.MyListing!.Property!.LocationString,
                    PropertyPrice = c.MyListing.Property.Price,
                    MyListingId = c.MyListingId
                })
                .ToListAsync(),

            // Never triaged — no MyListing exists for the property at all,
            // not just "not yet Interested".
            NewListingsPendingAction = await _db.Properties
                .CountAsync(p => p.CreatedAt > yesterday && p.MyListing == null),

            StatusBreakdown = await _db.MyListings
                .GroupBy(m => m.Status)
                .Select(g => new StatusCountDto { Status = g.Key, Count = g.Count() })
                .ToListAsync(),

            Platforms = await BuildPlatformStatusAsync()
        };

        return report;
    }

    private async Task<List<PlatformSyncStatusDto>> BuildPlatformStatusAsync()
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync();

        // GroupBy(...).Select(g => g.OrderByDescending(...).First()) doesn't
        // translate reliably against SQLite's EF provider (same class of
        // issue as AverageDecimalPriceAsync's decimal Average() below) — 200
        // is generous enough to cover all 6 sources' latest run even under
        // light usage, so picking "first per source" in-memory is safe and
        // avoids the translation risk entirely.
        var recentRuns = await _db.ScraperRuns
            .OrderByDescending(r => r.RunEndTime)
            .Take(200)
            .ToListAsync();

        var latestBySource = recentRuns
            .GroupBy(r => r.Source)
            .ToDictionary(g => g.Key, g => g.First());

        return KnownSources.Select(source =>
        {
            latestBySource.TryGetValue(source, out var lastRun);
            return new PlatformSyncStatusDto
            {
                Source = source,
                Enabled = IsScraperEnabled(source, settings),
                LastSyncedAt = lastRun?.RunEndTime,
                HasErrors = lastRun?.HasErrors ?? false,
                LastErrors = lastRun is { HasErrors: true }
                    ? MappingExtensions.DeserializeStringList(lastRun.ErrorsJson)
                    : new List<string>()
            };
        }).ToList();
    }

    // Mirrors ScraperService.IsScraperEnabled's source->flag mapping exactly.
    private static bool IsScraperEnabled(string source, AppSetting? settings)
    {
        if (settings == null) return false;
        return source switch
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

    public async Task<List<ScraperRunDto>> GetRecentRunsAsync(int take = 20)
    {
        var runs = await _db.ScraperRuns
            .OrderByDescending(r => r.RunStartTime)
            .Take(take)
            .ToListAsync();

        return runs.Select(r => r.ToDto()).ToList();
    }
}
