namespace EstateAggregator.DTOs;

public class PropertyDto
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Location { get; set; }
    public string? Distrito { get; set; }
    public string? Concelho { get; set; }
    public string? Freguesia { get; set; }
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
    public bool? Furnished { get; set; }
    public bool? AirConditioning { get; set; }
    public bool? Balcony { get; set; }
    public bool? Renovated { get; set; }
    public bool? Storage { get; set; }
    public bool? WaterView { get; set; }
    public bool? NearMetro { get; set; }
    public bool? HasUsageLicense { get; set; }
    public string? EnergyRating { get; set; }
    public string? Floor { get; set; }
    public int? TotalFloors { get; set; }
    public decimal? CondoFeeMonthly { get; set; }
    public bool? HasPool { get; set; }
    public bool? HasGarden { get; set; }
    public int? YearBuilt { get; set; }
    // Non-null once AiEnrichmentService.cs has processed this
    // property's description — the frontend uses this to relabel
    // "Description" as leftover/extra info rather than the full listing text.
    public DateTime? AiEnrichedAt { get; set; }
    public string? AgentName { get; set; }
    public string? AgentPhone { get; set; }
    public string? AgentEmail { get; set; }
    // The listing's own ID on its source site (e.g. ImoVirtual's numeric ad
    // ID, Idealista's propertyCode) — included in drafted messages so an
    // agent with many active listings can identify the exact one without
    // having to open the link first. Renamed from the model's
    // SourcePropertyId for API clarity; not every source populates it.
    public string? ExternalId { get; set; }
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
