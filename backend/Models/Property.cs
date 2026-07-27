namespace EstateAggregator.Models;

public class Property
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;

    public decimal? Price { get; set; }
    public string? LocationString { get; set; }
    // Portugal's administrative hierarchy (Distrito > Concelho > Freguesia),
    // parsed as far as each source's own data actually distinguishes them —
    // see LocationHierarchyParser. CustoJusto's API exposes all three
    // natively; Idealista exposes Distrito/Concelho/Freguesia as separate
    // fields too but Freguesia is only sometimes populated; ImoVirtual and
    // the HTML-scraped sources (CaixaImobiliario, Santander, CasaSapo) only
    // ever give two usable levels, so Freguesia stays null for those. Not
    // populated retroactively beyond a best-effort LocationString split for
    // rows scraped before this existed (see PropertiesController's
    // backfill-location-hierarchy endpoint) — a genuine re-scrape fills in
    // whatever finer level the source actually has.
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
    // "Em Construção" | "Nova Construção" | "Para Recuperar" | "Not Available"
    // — see ConstructionStatusExtractor.
    public string ConstructionStatus { get; set; } = "Not Available";
    // True/false when the description explicitly says so, null when not
    // mentioned — see ElevatorExtractor/ParkingExtractor/FurnishedExtractor/
    // AirConditioningExtractor/BalconyExtractor.
    public bool? Elevator { get; set; }
    public bool? Parking { get; set; }
    public bool? Furnished { get; set; }
    public bool? AirConditioning { get; set; }
    public bool? Balcony { get; set; }
    public bool? Renovated { get; set; }
    public bool? Storage { get; set; }
    public bool? WaterView { get; set; }
    public bool? NearMetro { get; set; }
    // false when the listing explicitly says there's no licença de
    // utilização/habitação (often a Simplex/DL 10-2024 sale, cash-only, no
    // mortgage) — see UsageLicenseExtractor. Null when not mentioned; true
    // is rare since having one is the unremarkable default case.
    public bool? HasUsageLicense { get; set; }
    // "A+" through "F" — see EnergyRatingExtractor. Null when not mentioned.
    public string? EnergyRating { get; set; }

    // Everything below is populated by AiEnrichmentService.cs, never by
    // a scraper's own MapToProperty — DeduplicationService.ProcessAsync
    // deliberately leaves these alone on re-scrape (see its comment) so a
    // twice-daily re-scrape of unchanged text doesn't wipe out prior
    // extraction. e.g. "2º" — free text, since Portuguese floors include
    // "R/C", "Cave", "Sótão", not just numbers.
    public string? Floor { get; set; }
    public int? TotalFloors { get; set; }
    public decimal? CondoFeeMonthly { get; set; }
    public bool? HasPool { get; set; }
    public bool? HasGarden { get; set; }
    public int? YearBuilt { get; set; }
    // Null until AiEnrichmentService.cs has processed this row's
    // Description at least once; set every time it runs (even if it found
    // nothing), so the script's own "still needs processing" query
    // (AiEnrichedAt IS NULL) doesn't retry a description with nothing left
    // to extract on every single run. Reset to null by
    // DeduplicationService.ProcessAsync whenever Description actually
    // changes, so a genuinely updated description gets re-processed.
    public DateTime? AiEnrichedAt { get; set; }

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
