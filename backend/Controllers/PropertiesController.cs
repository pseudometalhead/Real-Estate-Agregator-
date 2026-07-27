using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using EstateAggregator.Services;
using EstateAggregator.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    private readonly EstateDbContext _db;
    private readonly DeduplicationService _dedupService;

    public PropertiesController(EstateDbContext db, DeduplicationService dedupService)
    {
        _db = db;
        _dedupService = dedupService;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResult<PropertyDto>>> GetProperties([FromQuery] FilterQueryDto filter)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 12 : filter.PageSize;

        var query = _db.Properties.AsQueryable();

        if (filter.PriceMin.HasValue)
            query = query.Where(p => p.Price >= filter.PriceMin);

        if (filter.PriceMax.HasValue)
            query = query.Where(p => p.Price <= filter.PriceMax);

        if (filter.Beds.HasValue)
            query = query.Where(p => p.Beds == filter.Beds);

        if (!string.IsNullOrEmpty(filter.Orientation) && filter.Orientation != "All")
            query = query.Where(p => p.SunOrientation == filter.Orientation);

        // See FilterQueryDto.OpenPlanKitchen for why "No" means "!= true"
        // here specifically, unlike Elevator/Parking below.
        if (filter.OpenPlanKitchen == true)
            query = query.Where(p => p.OpenPlanKitchen == true);
        else if (filter.OpenPlanKitchen == false)
            query = query.Where(p => p.OpenPlanKitchen != true);

        if (!string.IsNullOrEmpty(filter.ConstructionStatus) && filter.ConstructionStatus != "All")
            query = query.Where(p => p.ConstructionStatus == filter.ConstructionStatus);

        if (filter.Elevator.HasValue)
            query = query.Where(p => p.Elevator == filter.Elevator);

        if (filter.Parking.HasValue)
            query = query.Where(p => p.Parking == filter.Parking);

        // Cast to double, same SQLite-decimal-translation workaround as the
        // ORDER BY below — decimal arithmetic doesn't reliably translate to
        // SQL here either.
        if (filter.PricePerM2Min.HasValue)
        {
            var min = (double)filter.PricePerM2Min.Value;
            query = query.Where(p => p.Price != null && p.SizeM2 != null && p.SizeM2 > 0
                && (double)p.Price.Value / (double)p.SizeM2.Value >= min);
        }

        if (filter.PricePerM2Max.HasValue)
        {
            var max = (double)filter.PricePerM2Max.Value;
            query = query.Where(p => p.Price != null && p.SizeM2 != null && p.SizeM2 > 0
                && (double)p.Price.Value / (double)p.SizeM2.Value <= max);
        }

        if (filter.AddedWithinDays.HasValue)
        {
            var cutoff = DateTime.UtcNow.AddDays(-filter.AddedWithinDays.Value);
            query = query.Where(p => p.CreatedAt >= cutoff);
        }

        if (filter.PendingActionOnly == true)
            query = query.Where(p => p.MyListing == null);

        if (!string.IsNullOrEmpty(filter.Location))
            query = query.Where(p => p.LocationString != null && p.LocationString.Contains(filter.Location));

        if (!string.IsNullOrEmpty(filter.Source))
            query = query.Where(p => p.Source == filter.Source);

        var total = await query.CountAsync();

        // SQLite's EF Core provider refuses to ORDER BY a `decimal` column
        // server-side (throws NotSupportedException) — Price/SizeM2 are
        // cast to double first, which SQLite can sort natively.
        var sortDescending = !string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "price" => sortDescending
                ? query.OrderByDescending(p => (double?)p.Price)
                : query.OrderBy(p => (double?)p.Price),
            "size" => sortDescending
                ? query.OrderByDescending(p => (double?)p.SizeM2)
                : query.OrderBy(p => (double?)p.SizeM2),
            _ => query.OrderByDescending(p => p.LastSeenAt)
        };

        var properties = await query
            .Include(p => p.PriceHistory)
            .Include(p => p.LinkedSources)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PaginatedResult<PropertyDto>
        {
            Items = properties.Select(p => p.ToDto()).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PropertyDto>> GetProperty(int id)
    {
        var property = await _db.Properties
            .Include(p => p.PriceHistory)
            .Include(p => p.LinkedSources)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (property == null)
            return NotFound();

        return Ok(property.ToDto());
    }

    // Manual entry point for sources with no live scraper (currently just
    // Idealista — see docs/idealista-integration-plan.md). Runs the same
    // orientation/open-plan-kitchen extraction and dedup pipeline every
    // scraper uses, so a manually-entered listing behaves identically to a
    // scraped one from here on.
    [HttpPost("import")]
    public async Task<ActionResult<ImportPropertyResultDto>> ImportManual([FromBody] ImportPropertyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Url))
            return BadRequest("Url is required.");

        var source = string.IsNullOrWhiteSpace(dto.Source) ? "Idealista" : dto.Source.Trim();
        var description = dto.Description ?? string.Empty;
        var (orientation, orientationSource) = OrientationExtractor.Extract(description);
        var openPlanKitchen = OpenPlanKitchenExtractor.Extract(description);
        var constructionStatus = ConstructionStatusExtractor.Extract(description);
        var elevator = ElevatorExtractor.Extract(description);
        var parking = ParkingExtractor.Extract(description);

        var candidate = new Property
        {
            Url = dto.Url.Trim(),
            Source = source,
            Price = dto.Price,
            LocationString = dto.LocationString,
            Beds = dto.Beds,
            Baths = dto.Baths,
            SizeM2 = dto.SizeM2,
            Description = description,
            SunOrientation = orientation,
            OrientationSource = orientationSource,
            OpenPlanKitchen = openPlanKitchen,
            ConstructionStatus = constructionStatus,
            Elevator = elevator,
            Parking = parking,
            PhotosJson = System.Text.Json.JsonSerializer.Serialize(dto.Photos ?? new List<string>()),
            DedupHash = DedupHashGenerator.Compute(dto.LocationString, dto.Price ?? 0, dto.Beds)
        };

        var outcome = await _dedupService.ProcessAsync(_db, candidate);
        await _db.SaveChangesAsync();

        if (outcome == DedupOutcome.Skipped)
            return Ok(new ImportPropertyResultDto { Outcome = "Skipped", Property = null });

        // ProcessAsync mutates an existing tracked row in place for the
        // Updated case rather than handing back a reference, so both
        // non-skipped outcomes are resolved the same way: re-query by the
        // now-saved Url — except Linked, where candidate.Url was never saved
        // to Properties (it only became a PropertySource on the existing
        // canonical row), so the canonical property is looked up by hash
        // instead. DedupHash isn't unique by design (it's a bucketed
        // approximate match — see DedupHashGenerator), so this takes the
        // most-recently-seen match, same tolerance the dedup window itself uses.
        var saved = outcome == DedupOutcome.Linked
            ? await _db.Properties
                .Include(p => p.PriceHistory)
                .Include(p => p.LinkedSources)
                .Where(p => p.DedupHash == candidate.DedupHash)
                .OrderByDescending(p => p.LastSeenAt)
                .FirstOrDefaultAsync()
            : await _db.Properties
                .Include(p => p.PriceHistory)
                .FirstOrDefaultAsync(p => p.Url == candidate.Url);

        return Ok(new ImportPropertyResultDto { Outcome = outcome.ToString(), Property = saved?.ToDto() });
    }
}
