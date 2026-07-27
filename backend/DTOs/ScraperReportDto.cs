namespace EstateAggregator.DTOs;

public class ScraperReportDto
{
    public string Source { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public int PropertiesFound { get; set; }
    public int PropertiesAdded { get; set; }
    public int PropertiesUpdated { get; set; }
    // Recognized as an existing property re-listed on another site and
    // linked as a PropertySource rather than discarded — see
    // DeduplicationService.ProcessAsync / DedupOutcome.Linked.
    public int PropertiesLinked { get; set; }
    public int PropertiesSkipped { get; set; }
    public bool HasErrors { get; set; }
    public List<string> Errors { get; set; } = new();

    public void Merge(ScraperReportDto other)
    {
        PropertiesFound += other.PropertiesFound;
        PropertiesAdded += other.PropertiesAdded;
        PropertiesUpdated += other.PropertiesUpdated;
        PropertiesLinked += other.PropertiesLinked;
        PropertiesSkipped += other.PropertiesSkipped;
        HasErrors = HasErrors || other.HasErrors;
        Errors.AddRange(other.Errors);
    }
}

public class ScraperRunDto
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
    public List<string> Errors { get; set; } = new();
    public int DurationSeconds { get; set; }
}
