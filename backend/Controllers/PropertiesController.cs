using EstateAggregator.Data;
using EstateAggregator.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    private readonly EstateDbContext _db;

    public PropertiesController(EstateDbContext db)
    {
        _db = db;
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

        if (!string.IsNullOrEmpty(filter.Location))
            query = query.Where(p => p.LocationString != null && p.LocationString.Contains(filter.Location));

        var total = await query.CountAsync();

        var sortDescending = !string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "price" => sortDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            "size" => sortDescending ? query.OrderByDescending(p => p.SizeM2) : query.OrderBy(p => p.SizeM2),
            _ => query.OrderByDescending(p => p.LastSeenAt)
        };

        var properties = await query
            .Include(p => p.PriceHistory)
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
            .FirstOrDefaultAsync(p => p.Id == id);

        if (property == null)
            return NotFound();

        return Ok(property.ToDto());
    }
}
