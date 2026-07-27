namespace EstateAggregator.Models;

// A second (or third...) place the same physical property is listed. When
// the scraper pipeline recognizes a new listing as a duplicate of an
// existing Property by DedupHash (same bucketed price/location/beds within
// the 30-day duplicate window) but at a different URL, it used to just be
// silently discarded (DedupOutcome.Skipped). Now it's linked here instead,
// so "listed on 3 sites" is visible and clickable rather than lost — see
// DeduplicationService.ProcessAsync.
public class PropertySource
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public Property? Property { get; set; }

    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public decimal? Price { get; set; }

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
