namespace EstateAggregator.Models;

public class AppSetting
{
    public int Id { get; set; }

    public string DistrictsJson { get; set; } = "[]";
    public decimal PriceMin { get; set; }
    public decimal PriceMax { get; set; }
    public int RoomsMin { get; set; }
    public int RoomsMax { get; set; }

    public bool ScrapeIdealistaEnabled { get; set; } = true;
    public bool ScrapeImoVirtualEnabled { get; set; } = true;
    public bool ScrapeImobiliarioEnabled { get; set; } = true;

    public string LogFilePath { get; set; } = "/app/logs/estate-aggregator.log";

    public DateTime? LastScrapedAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
