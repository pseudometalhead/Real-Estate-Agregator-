namespace EstateAggregator.Utilities;

// Best-effort split of a flat "A, B" location string into (Distrito,
// Concelho) — used by CasaSapoScraper, whose HTML only ever exposes a single
// combined location field with no separate structure, and by the
// backfill-location-hierarchy endpoint for properties scraped before
// Distrito/Concelho/Freguesia existed as their own columns. Every scraper
// already builds LocationString as "{district}, {something}" (or just
// "{district}" alone), so splitting on the first comma recovers the same two
// levels the UI was already showing — it doesn't invent a Freguesia this
// text doesn't actually distinguish from Concelho.
public static class LocationHierarchyParser
{
    public static (string? Distrito, string? Concelho) SplitFlat(string? locationString)
    {
        if (string.IsNullOrWhiteSpace(locationString) || locationString == "Unknown")
            return (null, null);

        var parts = locationString.Split(',', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], null);
    }
}
