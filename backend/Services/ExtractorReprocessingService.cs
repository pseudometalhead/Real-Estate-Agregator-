using EstateAggregator.Data;
using EstateAggregator.Utilities;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Services;

// Re-runs every regex-based fact extractor (Orientation, ConstructionStatus,
// Elevator, Parking, ...) against each property's already-stored Description
// — no scraper HTTP requests, no re-fetching anything from CustoJusto/
// ImoVirtual/etc. Exists for exactly the situation an extractor gets fixed
// or extended (e.g. OrientationExtractor learning to read "N/E"-style
// compass abbreviations) after thousands of properties are already scraped:
// this applies the fix to the existing data immediately instead of waiting
// for those listings to naturally get re-scraped.
//
// Deliberately skips any property AiEnrichmentService has already processed
// (AiEnrichedAt IS NOT NULL) — that service rewrites Description down to
// only the leftover info NOT already captured by these extractors, so
// re-running the extractors against that trimmed text would find far less
// than the original full description did and silently regress
// already-correct facts back to null/"Not Available".
public class ExtractorReprocessingService
{
    private readonly ILogger<ExtractorReprocessingService> _logger;

    public ExtractorReprocessingService(ILogger<ExtractorReprocessingService> logger)
    {
        _logger = logger;
    }

    public async Task<(int Processed, int Skipped)> ReprocessAsync(EstateDbContext db, CancellationToken cancellationToken = default)
    {
        var properties = await db.Properties
            .Where(p => p.AiEnrichedAt == null && p.Description != null && p.Description != "")
            .ToListAsync(cancellationToken);

        foreach (var property in properties)
        {
            // Re-cleaning is safe to run again on already-cleaned text — it's
            // idempotent — and picks up fixes made to DescriptionCleaner
            // itself (e.g. the HtmlAgilityPack astral-emoji-entity mangling
            // fix) without needing a re-scrape.
            property.Description = DescriptionCleaner.Clean(property.Description);
            var description = property.Description!;
            var (orientation, orientationSource) = OrientationExtractor.Extract(description);
            property.SunOrientation = orientation;
            property.OrientationSource = orientationSource;
            property.OpenPlanKitchen = OpenPlanKitchenExtractor.Extract(description);
            property.ConstructionStatus = ConstructionStatusExtractor.Extract(description);
            property.Elevator = ElevatorExtractor.Extract(description);
            property.Parking = ParkingExtractor.Extract(description);
            property.Furnished = FurnishedExtractor.Extract(description);
            property.AirConditioning = AirConditioningExtractor.Extract(description);
            property.Balcony = BalconyExtractor.Extract(description);
            property.Renovated = RenovatedExtractor.Extract(description);
            property.Storage = StorageExtractor.Extract(description);
            property.WaterView = WaterViewExtractor.Extract(description);
            property.NearMetro = NearMetroExtractor.Extract(description);
            property.HasUsageLicense = UsageLicenseExtractor.Extract(description);
            property.EnergyRating = EnergyRatingExtractor.Extract(description);
        }

        await db.SaveChangesAsync(cancellationToken);

        var skipped = await db.Properties.CountAsync(p => p.AiEnrichedAt != null, cancellationToken);
        _logger.LogInformation("Reprocessed extractors for {Processed} propert(y/ies), skipped {Skipped} already AI-enriched", properties.Count, skipped);
        return (properties.Count, skipped);
    }

    // One-off backfill for UsageLicenseExtractor specifically: unlike the
    // main pass above, this deliberately does NOT skip AI-enriched rows.
    // AiEnrichmentService trims Description down to leftover facts, but the
    // "sem licença de utilização / Simplex" note is exactly the kind of fact
    // it's instructed to preserve rather than discard, so re-scanning the
    // (already trimmed) Description text for this one field is safe and
    // doesn't risk regressing any of the other extractor fields the main
    // pass guards against re-running on enriched text.
    public async Task<int> BackfillUsageLicenseAsync(EstateDbContext db, CancellationToken cancellationToken = default)
    {
        var properties = await db.Properties
            .Where(p => p.HasUsageLicense == null && p.Description != null && p.Description != "")
            .ToListAsync(cancellationToken);

        foreach (var property in properties)
        {
            property.HasUsageLicense = UsageLicenseExtractor.Extract(property.Description);
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Backfilled HasUsageLicense for {Count} propert(y/ies)", properties.Count);
        return properties.Count;
    }

    // One-off backfill for Distrito/Concelho on properties scraped before
    // those columns existed — see LocationHierarchyParser for why this only
    // ever recovers two levels (Freguesia isn't distinguishable from
    // Concelho in an already-flattened LocationString). Safe to run
    // regardless of AiEnrichedAt, same reasoning as BackfillUsageLicenseAsync:
    // it only touches these two new fields, not anything AiEnrichmentService
    // owns.
    public async Task<int> BackfillLocationHierarchyAsync(EstateDbContext db, CancellationToken cancellationToken = default)
    {
        var properties = await db.Properties
            .Where(p => p.Distrito == null)
            .ToListAsync(cancellationToken);

        foreach (var property in properties)
        {
            var (distrito, concelho) = LocationHierarchyParser.SplitFlat(property.LocationString);
            property.Distrito = distrito;
            property.Concelho = concelho;
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Backfilled location hierarchy for {Count} propert(y/ies)", properties.Count);
        return properties.Count;
    }

    // One-off correction for properties whose Concelho field actually holds
    // a civil-parish (freguesia) name — the historic naive 2-way split of a
    // flat LocationString (see BackfillLocationHierarchyAsync above, and
    // every scraper's own LocationString construction before real
    // Municipality/District fields were captured separately) had no way to
    // tell a concelho from a freguesia, since the flat string only ever
    // combined "{district}, {something}" with no concelho-level information
    // preserved at all. PortoDistrictGeography resolves that "something"
    // against Porto district's real freguesia→concelho map and only ever
    // corrects unambiguous matches — see its own comment for why some
    // properties are deliberately left untouched rather than guessed.
    //
    // Runs regardless of whether Freguesia is already set: verified live
    // that Idealista's own "Municipality" API field isn't always genuinely
    // concelho-level — for some listings (typically well-known suburbs) it
    // returns a freguesia name while "District" separately returns an
    // even-finer neighborhood name, so Concelho can be wrong even when
    // Freguesia already holds legitimate (just finer-grained) data. Only
    // Concelho gets corrected in that case — an existing Freguesia value is
    // never overwritten, since it's more specific than anything this lookup
    // could produce, not wrong.
    public async Task<int> BackfillMisplacedConcelhoAsync(EstateDbContext db, CancellationToken cancellationToken = default)
    {
        var properties = await db.Properties
            .Where(p => p.Concelho != null)
            .ToListAsync(cancellationToken);

        var fixedCount = 0;
        foreach (var property in properties)
        {
            if (!PortoDistrictGeography.TryFixMisplacedConcelho(property.Concelho, out var correctConcelho, out var freguesia))
                continue;

            property.Concelho = correctConcelho;
            if (property.Freguesia == null)
                property.Freguesia = freguesia;
            fixedCount++;
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Corrected misplaced Concelho/Freguesia for {Count} propert(y/ies)", fixedCount);
        return fixedCount;
    }
}
