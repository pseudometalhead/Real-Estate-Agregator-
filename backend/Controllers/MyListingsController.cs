using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MyListingsController : ControllerBase
{
    private readonly EstateDbContext _db;

    public MyListingsController(EstateDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResult<MyListingDto>>> GetMyListings(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        var query = _db.MyListings
            .Include(ml => ml.Property).ThenInclude(p => p!.LinkedSources)
            .Include(ml => ml.CommHistory)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(ml => ml.Status == status);

        var total = await query.CountAsync();
        var listings = await query
            .OrderByDescending(ml => ml.LastUpdated)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PaginatedResult<MyListingDto>
        {
            Items = listings.Select(l => l.ToDto()).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MyListingDto>> GetMyListing(int id)
    {
        var listing = await _db.MyListings
            .Include(ml => ml.Property).ThenInclude(p => p!.LinkedSources)
            .Include(ml => ml.CommHistory)
            .FirstOrDefaultAsync(ml => ml.Id == id);
        if (listing == null)
            return NotFound();

        return Ok(listing.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<MyListingDto>> AddToMyListings([FromBody] CreateMyListingDto dto)
    {
        var property = await _db.Properties.FindAsync(dto.PropertyId);
        if (property == null)
            return NotFound("Property not found");

        var alreadyTracked = await _db.MyListings.AnyAsync(ml => ml.PropertyId == dto.PropertyId);
        if (alreadyTracked)
            return Conflict("Property is already being tracked");

        // Agent fields default to whatever the scraper already captured on the
        // Property (see PropertyAgentContact migration) rather than starting
        // blank — without this, every listing created via the swipe/❤️
        // one-tap flow (which never sends agent info in the request body)
        // would show no contact button at all despite the data already being
        // on file. An explicit dto value (e.g. a manual correction) still wins.
        var listing = new MyListing
        {
            PropertyId = dto.PropertyId,
            Status = string.IsNullOrEmpty(dto.Status) ? "Interested" : dto.Status,
            Notes = dto.Notes,
            AgentName = dto.AgentName ?? property.AgentName,
            AgentPhone = dto.AgentPhone ?? property.AgentPhone,
            AgentEmail = dto.AgentEmail ?? property.AgentEmail,
            DateAdded = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        _db.MyListings.Add(listing);
        await _db.SaveChangesAsync();

        listing.Property = property;
        return CreatedAtAction(nameof(GetMyListing), new { id = listing.Id }, listing.ToDto());
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateMyListing(int id, [FromBody] UpdateMyListingDto dto)
    {
        var listing = await _db.MyListings.FindAsync(id);
        if (listing == null)
            return NotFound();

        listing.Status = dto.Status;
        listing.Notes = dto.Notes;
        listing.AgentName = dto.AgentName;
        listing.AgentPhone = dto.AgentPhone;
        listing.AgentEmail = dto.AgentEmail;
        listing.AskedAboutOrientation = dto.AskedAboutOrientation ?? listing.AskedAboutOrientation;
        listing.AskedAboutOpenPlanKitchen = dto.AskedAboutOpenPlanKitchen ?? listing.AskedAboutOpenPlanKitchen;
        listing.FollowUpDate = dto.FollowUpDate;
        listing.LastUpdated = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteMyListing(int id)
    {
        var listing = await _db.MyListings.FindAsync(id);
        if (listing == null)
            return NotFound();

        _db.MyListings.Remove(listing);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
