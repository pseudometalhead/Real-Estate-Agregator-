using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Controllers;

// Manual trigger for the scraper orchestration pipeline. Useful for testing
// end-to-end wiring (dedup, ScraperRun logging, report generation) without
// waiting for the scheduled 8am/5pm Quartz cron, and will remain useful once
// real scraper integrations exist.
[ApiController]
[Route("api/[controller]")]
public class ScrapersController : ControllerBase
{
    private readonly ScraperService _scraperService;
    private readonly GeocodingService _geocodingService;
    private readonly EstateDbContext _db;

    public ScrapersController(ScraperService scraperService, GeocodingService geocodingService, EstateDbContext db)
    {
        _scraperService = scraperService;
        _geocodingService = geocodingService;
        _db = db;
    }

    [HttpPost("run-now")]
    public async Task<ActionResult<ScraperReportDto>> RunNow(CancellationToken cancellationToken)
    {
        var report = await _scraperService.RunScrapersAsync(cancellationToken);
        return Ok(report);
    }

    // Lets a geocoding backfill be triggered on its own, without re-running
    // every scraper — useful right after raising MaxLocationsPerRun, or any
    // time a backlog of un-geocoded properties builds up (e.g. Nominatim was
    // briefly unreachable during a scrape run).
    [HttpPost("geocode-now")]
    public async Task<ActionResult<object>> GeocodeNow(CancellationToken cancellationToken)
    {
        var missingBefore = await _db.Properties.CountAsync(p => p.Lat == null, cancellationToken);

        await _geocodingService.GeocodeMissingAsync(_db, cancellationToken);

        var total = await _db.Properties.CountAsync(cancellationToken);
        var stillMissing = await _db.Properties.CountAsync(p => p.Lat == null, cancellationToken);

        return Ok(new
        {
            totalProperties = total,
            stillMissingCoordinates = stillMissing,
            geocoded = missingBefore - stillMissing
        });
    }
}
