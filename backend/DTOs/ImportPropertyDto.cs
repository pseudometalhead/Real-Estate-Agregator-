namespace EstateAggregator.DTOs;

// A hand-entered listing, for sources with no live scraper — today that's
// specifically Idealista (blocked by DataDome bot protection; see
// docs/idealista-integration-plan.md). The user copies the details off a
// listing they're viewing in their own browser; this runs it through the
// same dedup/orientation/open-plan-kitchen pipeline every scraper uses, so
// it behaves identically to a scraped property once saved.
public class ImportPropertyDto
{
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = "Idealista";
    public decimal? Price { get; set; }
    public string? LocationString { get; set; }
    public int? Beds { get; set; }
    public int? Baths { get; set; }
    public decimal? SizeM2 { get; set; }
    public string? Description { get; set; }
    public List<string>? Photos { get; set; }
}

public class ImportPropertyResultDto
{
    public string Outcome { get; set; } = string.Empty; // "Added" | "Updated" | "Skipped"
    public PropertyDto? Property { get; set; }
}
