namespace EstateAggregator.DTOs;

public class PropertyDto
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Location { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public int? Beds { get; set; }
    public int? Baths { get; set; }
    public decimal? SizeM2 { get; set; }
    public string? Description { get; set; }
    public string SunOrientation { get; set; } = "Not Available";
    public string OrientationSource { get; set; } = "not_available";
    public bool? OpenPlanKitchen { get; set; }
    public string ConstructionStatus { get; set; } = "Not Available";
    public bool? Elevator { get; set; }
    public bool? Parking { get; set; }
    public string? AgentName { get; set; }
    public string? AgentPhone { get; set; }
    public string? AgentEmail { get; set; }
    public List<string> Photos { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    // Populated from the most recent PriceHistoryEntry, if any.
    public decimal? PreviousPrice { get; set; }
    public DateTime? PriceChangedAt { get; set; }
    public List<PriceHistoryPointDto> PriceHistory { get; set; } = new();

    // Other sites this same physical property is also listed on — see
    // PropertySource / DeduplicationService. Empty for a property only ever
    // seen on one site.
    public List<PropertySourceDto> LinkedSources { get; set; } = new();
}

public class PriceHistoryPointDto
{
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class PropertySourceDto
{
    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public DateTime LastSeenAt { get; set; }
}
