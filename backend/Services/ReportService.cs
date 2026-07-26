using EstateAggregator.Data;
using EstateAggregator.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Services;

public class ReportService
{
    private readonly EstateDbContext _db;

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
            PropertiesAddedLast24h = await _db.Properties.CountAsync(p => p.CreatedAt > yesterday),
            PropertiesUpdatedLast24h = await _db.Properties
                .CountAsync(p => p.LastSeenAt > yesterday && p.CreatedAt <= yesterday),

            ScraperRuns = (await _db.ScraperRuns
                .Where(sr => sr.RunStartTime > yesterday)
                .OrderBy(sr => sr.Source)
                .ToListAsync())
                .Select(sr => sr.ToDto())
                .ToList(),

            TopOrientations = await _db.Properties
                .GroupBy(p => p.SunOrientation)
                .Select(g => new OrientationCountDto { Orientation = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(),

            // SQLite's EF Core provider can't translate Average() over a decimal
            // column server-side, so the prices are pulled client-side first.
            AveragePrice = await AverageDecimalPriceAsync()
        };

        return report;
    }

    private async Task<decimal?> AverageDecimalPriceAsync()
    {
        var prices = await _db.Properties
            .Where(p => p.Price != null)
            .Select(p => p.Price!.Value)
            .ToListAsync();

        return prices.Count == 0 ? null : prices.Average();
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
