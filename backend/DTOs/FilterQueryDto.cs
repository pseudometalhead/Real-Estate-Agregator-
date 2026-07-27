namespace EstateAggregator.DTOs;

public class FilterQueryDto
{
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public int? Beds { get; set; }
    public string? Orientation { get; set; }
    // Tri-state: true = only Yes, false = only No, null = no filter (Both).
    // For OpenPlanKitchen specifically, "No" means "not confirmed open"
    // (Property.OpenPlanKitchen != true) rather than a confirmed-closed
    // fact — the extractor never returns a hard false for this one field,
    // see OpenPlanKitchenExtractor's own comment on why. Elevator/Parking
    // are true tri-states (their extractors do detect explicit negatives),
    // so "No" there means only listings explicitly saying so.
    public bool? OpenPlanKitchen { get; set; }
    public string? ConstructionStatus { get; set; }
    public bool? Elevator { get; set; }
    public bool? Parking { get; set; }
    public decimal? PricePerM2Min { get; set; }
    public decimal? PricePerM2Max { get; set; }
    // Only include properties first added within the last N days.
    public int? AddedWithinDays { get; set; }
    // Only include properties with no MyListing at all — never triaged
    // (not Interested, not Rejected, nothing).
    public bool? PendingActionOnly { get; set; }
    public string? Location { get; set; }
    public string? Source { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;

    // "date" (default), "price", "size"
    public string? SortBy { get; set; }
    // "asc" or "desc" (default depends on SortBy: "date" defaults desc, others asc)
    public string? SortDir { get; set; }
}
