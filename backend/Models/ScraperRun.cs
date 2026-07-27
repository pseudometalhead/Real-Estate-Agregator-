namespace EstateAggregator.Models;

public class ScraperRun
{
    public int Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime RunStartTime { get; set; }
    public DateTime? RunEndTime { get; set; }

    public int PropertiesFound { get; set; }
    public int PropertiesAdded { get; set; }
    public int PropertiesUpdated { get; set; }
    public int PropertiesLinked { get; set; }
    public int PropertiesSkipped { get; set; }

    public bool HasErrors { get; set; }
    public string ErrorsJson { get; set; } = "[]";

    public int DurationSeconds { get; set; }
}
