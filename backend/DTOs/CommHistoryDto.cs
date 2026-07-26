namespace EstateAggregator.DTOs;

public class CommHistoryEntryDto
{
    public int Id { get; set; }
    public int MyListingId { get; set; }
    public string Channel { get; set; } = "Email";
    public string Direction { get; set; } = "Outbound";
    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateCommHistoryEntryDto
{
    public string Channel { get; set; } = "Email";
    public string Direction { get; set; } = "Outbound";
    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;
    // Optional: caller can backdate a logged entry (e.g. logging a call that
    // already happened). Defaults to now if omitted.
    public DateTime? CreatedAt { get; set; }
}
