namespace EstateAggregator.DTOs;

// Manual, no-API-cost alternative to AiEnrichmentService: GET pending-enrichment
// hands a batch of raw descriptions to whoever's reading (in practice, the
// coding assistant working in this repo, already covered by the user's
// existing session — no separate Anthropic API billing), which extracts the
// same facts AiEnrichmentService's prompt describes and posts them back via
// POST apply-enrichment. See ScrapersController for both endpoints.
public class PendingEnrichmentDto
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class ApplyEnrichmentDto
{
    public int Id { get; set; }
    public string? Floor { get; set; }
    public int? TotalFloors { get; set; }
    public decimal? CondoFeeMonthly { get; set; }
    public bool? HasPool { get; set; }
    public bool? HasGarden { get; set; }
    public int? YearBuilt { get; set; }
    // The description rewritten down to leftover info only — same meaning
    // as AiEnrichmentService's "extraInfo". Null/empty clears Description.
    public string? ExtraInfo { get; set; }
}
