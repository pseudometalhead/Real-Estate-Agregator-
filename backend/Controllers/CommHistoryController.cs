using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Controllers;

// Logs communication history against a tracked listing (MyListing). This app
// never sends messages to agents/sellers on its own — see the frontend's
// ContactPanel, which drafts a message and hands off to the user's own email
// client via a mailto: link. These endpoints only record what the user
// already did (or received), so there's a real audit trail without any
// unsupervised outbound contact to third parties.
[ApiController]
[Route("api/mylistings/{myListingId}/comms")]
public class CommHistoryController : ControllerBase
{
    private readonly EstateDbContext _db;

    public CommHistoryController(EstateDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<CommHistoryEntryDto>>> GetHistory(int myListingId)
    {
        var exists = await _db.MyListings.AnyAsync(l => l.Id == myListingId);
        if (!exists)
            return NotFound();

        var entries = await _db.CommHistoryEntries
            .Where(c => c.MyListingId == myListingId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Ok(entries.Select(c => c.ToDto()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CommHistoryEntryDto>> AddEntry(int myListingId, [FromBody] CreateCommHistoryEntryDto dto)
    {
        var listing = await _db.MyListings.FindAsync(myListingId);
        if (listing == null)
            return NotFound();

        var entry = new CommHistoryEntry
        {
            MyListingId = myListingId,
            Channel = dto.Channel,
            Direction = dto.Direction,
            Subject = dto.Subject,
            Message = dto.Message,
            CreatedAt = dto.CreatedAt ?? DateTime.UtcNow
        };

        _db.CommHistoryEntries.Add(entry);
        listing.LastUpdated = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetHistory), new { myListingId }, entry.ToDto());
    }

    [HttpDelete("{commId}")]
    public async Task<ActionResult> DeleteEntry(int myListingId, int commId)
    {
        var entry = await _db.CommHistoryEntries
            .FirstOrDefaultAsync(c => c.Id == commId && c.MyListingId == myListingId);

        if (entry == null)
            return NotFound();

        _db.CommHistoryEntries.Remove(entry);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
