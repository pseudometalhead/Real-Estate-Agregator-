namespace EstateAggregator.DTOs;

public class MyListingDto
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public PropertyDto? Property { get; set; }

    public string Status { get; set; } = "Interested";
    public string? Notes { get; set; }

    public string? AgentName { get; set; }
    public string? AgentPhone { get; set; }
    public string? AgentEmail { get; set; }

    public bool AskedAboutOrientation { get; set; }
    public DateTime? FollowUpDate { get; set; }

    public DateTime DateAdded { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class CreateMyListingDto
{
    public int PropertyId { get; set; }
    public string? Notes { get; set; }
    public string? AgentName { get; set; }
    public string? AgentPhone { get; set; }
    public string? AgentEmail { get; set; }
}

public class UpdateMyListingDto
{
    public string Status { get; set; } = "Interested";
    public string? Notes { get; set; }
    public string? AgentName { get; set; }
    public string? AgentPhone { get; set; }
    public string? AgentEmail { get; set; }
    public bool? AskedAboutOrientation { get; set; }
    public DateTime? FollowUpDate { get; set; }
}
