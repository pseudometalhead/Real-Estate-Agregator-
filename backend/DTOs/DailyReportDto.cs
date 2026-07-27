namespace EstateAggregator.DTOs;

public class DailyReportDto
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public int ContactsMadeLast24h { get; set; }
    // Last 5 outbound contacts, for a quick "who did I contact" glance.
    public List<ContactSummaryDto> RecentContacts { get; set; } = new();

    // Properties added in the last 24h that have no MyListing at all yet —
    // never triaged (not Interested, not Rejected, nothing).
    public int NewListingsPendingAction { get; set; }

    // All-time snapshot across every tracked listing, not 24h-scoped —
    // "where do things stand overall" is a different question than "what
    // happened today".
    public List<StatusCountDto> StatusBreakdown { get; set; } = new();

    public List<PlatformSyncStatusDto> Platforms { get; set; } = new();
}

public class ContactSummaryDto
{
    public string Channel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? PropertyLocation { get; set; }
    public decimal? PropertyPrice { get; set; }
    public int MyListingId { get; set; }
}

public class StatusCountDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class PlatformSyncStatusDto
{
    public string Source { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    // null = this source has never completed a scrape run.
    public DateTime? LastSyncedAt { get; set; }
    // From that last run — meaningless (always false) when LastSyncedAt is null.
    public bool HasErrors { get; set; }
}
