namespace EstateAggregator.Models;

public class CommHistoryEntry
{
    public int Id { get; set; }
    public int MyListingId { get; set; }
    public MyListing? MyListing { get; set; }

    // "Email" | "Phone" | "Site" | "InPerson" | "Other"
    public string Channel { get; set; } = "Email";
    // "Outbound" | "Inbound"
    public string Direction { get; set; } = "Outbound";

    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
