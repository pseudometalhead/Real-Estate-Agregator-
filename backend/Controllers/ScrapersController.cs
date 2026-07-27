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
    private readonly ExtractorReprocessingService _reprocessingService;
    private readonly AiEnrichmentService _aiEnrichmentService;
    private readonly EstateDbContext _db;

    public ScrapersController(
        ScraperService scraperService, GeocodingService geocodingService,
        ExtractorReprocessingService reprocessingService, AiEnrichmentService aiEnrichmentService, EstateDbContext db)
    {
        _scraperService = scraperService;
        _geocodingService = geocodingService;
        _reprocessingService = reprocessingService;
        _aiEnrichmentService = aiEnrichmentService;
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

    // Re-runs the regex extractors (Orientation, ConstructionStatus,
    // Elevator, ...) against every already-stored Description — no network
    // requests to any scraped site. Use this right after fixing/extending an
    // extractor to apply it to properties already in the database, instead
    // of waiting for them to be re-scraped naturally.
    [HttpPost("reprocess-extractors")]
    public async Task<ActionResult<object>> ReprocessExtractors(CancellationToken cancellationToken)
    {
        var (processed, skipped) = await _reprocessingService.ReprocessAsync(_db, cancellationToken);
        return Ok(new { processed, skippedAlreadyAiEnriched = skipped });
    }

    // One-off backfill for the HasUsageLicense field specifically — unlike
    // reprocess-extractors above, this also covers AI-enriched properties
    // (see BackfillUsageLicenseAsync's own comment for why that's safe here).
    [HttpPost("backfill-usage-license")]
    public async Task<ActionResult<object>> BackfillUsageLicense(CancellationToken cancellationToken)
    {
        var processed = await _reprocessingService.BackfillUsageLicenseAsync(_db, cancellationToken);
        return Ok(new { processed });
    }

    // One-off backfill for the Distrito/Concelho fields on properties
    // scraped before those columns existed.
    [HttpPost("backfill-location-hierarchy")]
    public async Task<ActionResult<object>> BackfillLocationHierarchy(CancellationToken cancellationToken)
    {
        var processed = await _reprocessingService.BackfillLocationHierarchyAsync(_db, cancellationToken);
        return Ok(new { processed });
    }

    // One-off correction for properties whose Concelho field actually holds
    // a freguesia name (see BackfillMisplacedConcelhoAsync's own comment) —
    // safe to re-run any time, since it only ever touches rows where
    // Freguesia is still null and only corrects unambiguous matches.
    [HttpPost("backfill-misplaced-concelho")]
    public async Task<ActionResult<object>> BackfillMisplacedConcelho(CancellationToken cancellationToken)
    {
        var processed = await _reprocessingService.BackfillMisplacedConcelhoAsync(_db, cancellationToken);
        return Ok(new { processed });
    }

    // Runs the AI fact-extraction pass (AiEnrichmentService) on its own,
    // without a full re-scrape — useful for draining a backlog faster than
    // waiting on the next scheduled scrape's bounded per-run batch, or for
    // verifying ANTHROPIC_API_KEY is actually working.
    [HttpPost("enrich-now")]
    public async Task<ActionResult<object>> EnrichNow(CancellationToken cancellationToken)
    {
        var pendingBefore = await _db.Properties.CountAsync(p => p.AiEnrichedAt == null && p.Description != null && p.Description != "", cancellationToken);

        await _aiEnrichmentService.RunAsync(cancellationToken);

        var pendingAfter = await _db.Properties.CountAsync(p => p.AiEnrichedAt == null && p.Description != null && p.Description != "", cancellationToken);

        return Ok(new { pendingBefore, pendingAfter, enriched = pendingBefore - pendingAfter });
    }

    // Manual, no-cost alternative to enrich-now: hands back raw
    // {id, description} pairs for whoever's going to do the extraction
    // themselves (see EnrichmentDtos.cs) instead of paying for the Anthropic
    // API. Same backlog AiEnrichmentService would have drawn from
    // (AiEnrichedAt IS NULL), same default batch size.
    [HttpGet("pending-enrichment")]
    public async Task<ActionResult<List<PendingEnrichmentDto>>> GetPendingEnrichment(
        [FromQuery] int limit = 40, CancellationToken cancellationToken = default)
    {
        var pending = await _db.Properties
            .Where(p => p.AiEnrichedAt == null && p.Description != null && p.Description != "")
            .OrderBy(p => p.Id)
            .Take(limit)
            .Select(p => new PendingEnrichmentDto { Id = p.Id, Description = p.Description! })
            .ToListAsync(cancellationToken);

        return Ok(pending);
    }

    // Writes back facts extracted by hand (or by whoever called
    // pending-enrichment) for a batch of properties, exactly like
    // AiEnrichmentService.RunAsync would have — same fields, same
    // AiEnrichedAt stamp, so DeduplicationService's "don't touch an
    // AI-enriched row" logic still applies going forward. Unknown/missing
    // IDs are silently skipped rather than erroring the whole batch.
    [HttpPost("apply-enrichment")]
    public async Task<ActionResult<object>> ApplyEnrichment(
        [FromBody] List<ApplyEnrichmentDto> facts, CancellationToken cancellationToken)
    {
        var ids = facts.Select(f => f.Id).ToList();
        var properties = await _db.Properties
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var applied = 0;
        foreach (var f in facts)
        {
            if (!properties.TryGetValue(f.Id, out var property))
                continue;

            property.Floor = f.Floor;
            property.TotalFloors = f.TotalFloors;
            property.CondoFeeMonthly = f.CondoFeeMonthly;
            property.HasPool = f.HasPool;
            property.HasGarden = f.HasGarden;
            property.YearBuilt = f.YearBuilt;
            property.Description = string.IsNullOrEmpty(f.ExtraInfo) ? null : f.ExtraInfo;
            property.AiEnrichedAt = DateTime.UtcNow;
            applied++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { applied, skipped = facts.Count - applied });
    }
}
