namespace EstateAggregator.DTOs;

public class PropertyDto
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Location { get; set; }
    public int? Beds { get; set; }
    public int? Baths { get; set; }
    public decimal? SizeM2 { get; set; }
    public string? Description { get; set; }
    public string SunOrientation { get; set; } = "Not Available";
    public string OrientationSource { get; set; } = "not_available";
    public List<string> Photos { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
