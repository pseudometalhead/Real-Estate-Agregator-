using EstateAggregator.Data;
using EstateAggregator.Models;
using EstateAggregator.Utilities;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Services;

public enum DedupOutcome
{
    Added,
    Updated,
    // Recognized as the same physical property already tracked under a
    // different URL/source, and linked as an additional PropertySource
    // rather than discarded — see ProcessAsync.
    Linked,
    Skipped
}

public class DeduplicationService
{
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromDays(30);

    public string GenerateDedupHash(string? locationString, decimal price, int? beds)
        => DedupHashGenerator.Compute(locationString, price, beds);

    // Applies the dedup rule this app uses:
    // 0. Already added-but-unsaved earlier in this same scrape run (e.g. a
    //    sponsored listing repeated within one page of real results) ->
    //    Skipped. Checked against the change tracker, not the database,
    //    since a plain query wouldn't see not-yet-saved pending inserts —
    //    without this, two same-run duplicates both get INSERTed and violate
    //    the Url unique constraint.
    // 1. Exact URL match -> refresh LastSeenAt / fields on the existing row (Updated).
    // 2. No URL match but the dedup hash matches a row seen within the last 30 days
    //    -> the same physical property, re-listed on another portal. Previously
    //    this was silently Skipped; now it's linked as a PropertySource on the
    //    existing (canonical) property instead, so "also listed on X" is visible
    //    rather than discarded (Linked).
    // 3. Otherwise -> insert as a new property (Added).
    public async Task<DedupOutcome> ProcessAsync(EstateDbContext db, Property candidate)
    {
        var alreadyPendingInThisBatch = db.ChangeTracker.Entries<Property>()
            .Any(e => e.State == EntityState.Added &&
                      (e.Entity.Url == candidate.Url || e.Entity.DedupHash == candidate.DedupHash));

        if (alreadyPendingInThisBatch)
            return DedupOutcome.Skipped;

        var existingByUrl = await db.Properties.FirstOrDefaultAsync(p => p.Url == candidate.Url);
        if (existingByUrl != null)
        {
            if (existingByUrl.Price.HasValue && candidate.Price.HasValue && existingByUrl.Price != candidate.Price)
            {
                db.PriceHistoryEntries.Add(new PriceHistoryEntry
                {
                    PropertyId = existingByUrl.Id,
                    OldPrice = existingByUrl.Price.Value,
                    NewPrice = candidate.Price.Value,
                    ChangedAt = DateTime.UtcNow
                });
            }

            existingByUrl.Price = candidate.Price;
            existingByUrl.LocationString = candidate.LocationString;
            existingByUrl.Beds = candidate.Beds;
            existingByUrl.Baths = candidate.Baths;
            existingByUrl.SizeM2 = candidate.SizeM2;
            existingByUrl.Description = candidate.Description;
            existingByUrl.SunOrientation = candidate.SunOrientation;
            existingByUrl.OrientationSource = candidate.OrientationSource;
            existingByUrl.OpenPlanKitchen = candidate.OpenPlanKitchen;
            existingByUrl.ConstructionStatus = candidate.ConstructionStatus;
            existingByUrl.Elevator = candidate.Elevator;
            existingByUrl.Parking = candidate.Parking;
            // Unlike every other field above, agent contact info is only
            // ever populated opportunistically — some scrapers fetch it via
            // an extra per-listing detail-page request made just once, the
            // first time a property is seen (see ImoVirtualScraper), so a
            // re-scrape's candidate legitimately has nulls here even when
            // the real values are already known. Overwriting unconditionally
            // would silently erase a previously-captured phone/email/name on
            // every later re-scrape. Only overwrite when the candidate
            // actually has something newer to say.
            if (!string.IsNullOrEmpty(candidate.AgentName)) existingByUrl.AgentName = candidate.AgentName;
            if (!string.IsNullOrEmpty(candidate.AgentPhone)) existingByUrl.AgentPhone = candidate.AgentPhone;
            if (!string.IsNullOrEmpty(candidate.AgentEmail)) existingByUrl.AgentEmail = candidate.AgentEmail;
            existingByUrl.PhotosJson = candidate.PhotosJson;
            existingByUrl.DedupHash = candidate.DedupHash;
            existingByUrl.LastSeenAt = DateTime.UtcNow;
            return DedupOutcome.Updated;
        }

        var cutoff = DateTime.UtcNow - DuplicateWindow;
        var canonical = await db.Properties
            .FirstOrDefaultAsync(p => p.DedupHash == candidate.DedupHash && p.LastSeenAt >= cutoff);

        if (canonical != null)
        {
            // Upsert the PropertySource: check the change tracker first (a
            // pending, not-yet-saved Add from earlier in this same batch —
            // same reasoning as the batch guard above, a DB query alone
            // wouldn't see it) before falling back to the database.
            var pendingSource = db.ChangeTracker.Entries<PropertySource>()
                .Select(e => e.Entity)
                .FirstOrDefault(s => s.PropertyId == canonical.Id && s.Url == candidate.Url);

            var existingSource = pendingSource ?? await db.PropertySources
                .FirstOrDefaultAsync(s => s.PropertyId == canonical.Id && s.Url == candidate.Url);

            if (existingSource != null)
            {
                existingSource.Price = candidate.Price;
                existingSource.LastSeenAt = DateTime.UtcNow;
            }
            else
            {
                db.PropertySources.Add(new PropertySource
                {
                    PropertyId = canonical.Id,
                    Source = candidate.Source,
                    Url = candidate.Url,
                    Price = candidate.Price,
                    FirstSeenAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow
                });
            }

            return DedupOutcome.Linked;
        }

        candidate.CreatedAt = DateTime.UtcNow;
        candidate.LastSeenAt = DateTime.UtcNow;
        candidate.FirstScrapedAt = DateTime.UtcNow;
        db.Properties.Add(candidate);
        return DedupOutcome.Added;
    }
}
