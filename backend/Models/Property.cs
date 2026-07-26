namespace EstateAggregator.Models;

public class Property
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;

    public decimal? Price { get; set; }
    public string? LocationString { get; set; }
    public int? Beds { get; set; }
    public int? Baths { get; set; }
    public decimal? SizeM2 { get; set; }

    public string? Description { get; set; }
    public string SunOrientation { get; set; } = "Not Available";
    public string OrientationSource { get; set; } = "not_available";

    public string PhotosJson { get; set; } = "[]";

    public string? SourcePropertyId { get; set; }
    public string DedupHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime? FirstScrapedAt { get; set; }

    public MyListing? MyListing { get; set; }
    public ICollection<PriceHistoryEntry> PriceHistory { get; set; } = new List<PriceHistoryEntry>();
}
