namespace EstateAggregator.DTOs;

public class DailyReportDto
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int PropertiesAddedLast24h { get; set; }
    public int PropertiesUpdatedLast24h { get; set; }
    public List<ScraperRunDto> ScraperRuns { get; set; } = new();
    public List<OrientationCountDto> TopOrientations { get; set; } = new();
    public decimal? AveragePrice { get; set; }
}

public class OrientationCountDto
{
    public string Orientation { get; set; } = string.Empty;
    public int Count { get; set; }
}
