namespace EstateAggregator.Models;

public class MyListing
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public Property? Property { get; set; }

    public string Status { get; set; } = "Interested";
    public string? Notes { get; set; }

    public string? AgentName { get; set; }
    public string? AgentPhone { get; set; }
    public string? AgentEmail { get; set; }

    public bool AskedAboutOrientation { get; set; }
    public bool AskedAboutOpenPlanKitchen { get; set; }
    public DateTime? FollowUpDate { get; set; }

    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public ICollection<CommHistoryEntry> CommHistory { get; set; } = new List<CommHistoryEntry>();
}
