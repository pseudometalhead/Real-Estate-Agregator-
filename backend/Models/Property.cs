namespace EstateAggregator.Models;

public class Property
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;

    public decimal? Price { get; set; }
    public string? LocationString { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public int? Beds { get; set; }
    public int? Baths { get; set; }
    public decimal? SizeM2 { get; set; }

    public string? Description { get; set; }
    public string SunOrientation { get; set; } = "Not Available";
    public string OrientationSource { get; set; } = "not_available";
    public bool? OpenPlanKitchen { get; set; }
    // "Em Construção" | "Nova Construção" | "Para Recuperar" | "Not Available"
    // — see ConstructionStatusExtractor.
    public string ConstructionStatus { get; set; } = "Not Available";
    // True/false when the description explicitly says so, null when not
    // mentioned — see ElevatorExtractor/ParkingExtractor.
    public bool? Elevator { get; set; }
    public bool? Parking { get; set; }

    public string PhotosJson { get; set; } = "[]";

    // Only Idealista populates these today (see IdealistaScraper.MapToProperty)
    // — every other scraper leaves them null, which MyListingModal already
    // treats as "no auto-fill" the same way it does for a listing with no
    // contact info at all.
    public string? AgentName { get; set; }
    public string? AgentPhone { get; set; }
    public string? AgentEmail { get; set; }

    public string? SourcePropertyId { get; set; }
    public string DedupHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime? FirstScrapedAt { get; set; }

    public MyListing? MyListing { get; set; }
    public ICollection<PriceHistoryEntry> PriceHistory { get; set; } = new List<PriceHistoryEntry>();
    // Other sites the same physical property is also listed on — see
    // PropertySource and DeduplicationService.ProcessAsync.
    public ICollection<PropertySource> LinkedSources { get; set; } = new List<PropertySource>();
}
