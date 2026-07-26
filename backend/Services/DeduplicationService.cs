using EstateAggregator.Data;
using EstateAggregator.Models;
using EstateAggregator.Utilities;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Services;

public enum DedupOutcome
{
    Added,
    Updated,
    Skipped
}

public class DeduplicationService
{
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromDays(30);

    public string GenerateDedupHash(string? locationString, decimal price, int? beds)
        => DedupHashGenerator.Compute(locationString, price, beds);

    // Applies the three-way dedup rule described in the MVP spec:
    // 1. Exact URL match -> refresh LastSeenAt / fields on the existing row (Updated).
    // 2. No URL match but the dedup hash matches a row seen within the last 30 days
    //    -> treat as the same property re-listed on another portal (Skipped).
    // 3. Otherwise -> insert as a new property (Added).
    public async Task<DedupOutcome> ProcessAsync(EstateDbContext db, Property candidate)
    {
        var existingByUrl = await db.Properties.FirstOrDefaultAsync(p => p.Url == candidate.Url);
        if (existingByUrl != null)
        {
            existingByUrl.Price = candidate.Price;
            existingByUrl.LocationString = candidate.LocationString;
            existingByUrl.Beds = candidate.Beds;
            existingByUrl.Baths = candidate.Baths;
            existingByUrl.SizeM2 = candidate.SizeM2;
            existingByUrl.Description = candidate.Description;
            existingByUrl.SunOrientation = candidate.SunOrientation;
            existingByUrl.OrientationSource = candidate.OrientationSource;
            existingByUrl.PhotosJson = candidate.PhotosJson;
            existingByUrl.DedupHash = candidate.DedupHash;
            existingByUrl.LastSeenAt = DateTime.UtcNow;
            return DedupOutcome.Updated;
        }

        var cutoff = DateTime.UtcNow - DuplicateWindow;
        var isDuplicate = await db.Properties
            .AnyAsync(p => p.DedupHash == candidate.DedupHash && p.LastSeenAt >= cutoff);

        if (isDuplicate)
            return DedupOutcome.Skipped;

        candidate.CreatedAt = DateTime.UtcNow;
        candidate.LastSeenAt = DateTime.UtcNow;
        candidate.FirstScrapedAt = DateTime.UtcNow;
        db.Properties.Add(candidate);
        return DedupOutcome.Added;
    }
}
