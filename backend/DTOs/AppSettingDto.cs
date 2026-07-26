namespace EstateAggregator.DTOs;

public class AppSettingDto
{
    public List<string> Districts { get; set; } = new();
    public decimal PriceMin { get; set; }
    public decimal PriceMax { get; set; }
    public int RoomsMin { get; set; }
    public int RoomsMax { get; set; }
    public bool ScrapeIdealistaEnabled { get; set; }
    public bool ScrapeImoVirtualEnabled { get; set; }
    public bool ScrapeImobiliarioEnabled { get; set; }
    public DateTime? LastScrapedAt { get; set; }
}

public class UpdateAppSettingDto
{
    public List<string> Districts { get; set; } = new();
    public decimal PriceMin { get; set; }
    public decimal PriceMax { get; set; }
    public int RoomsMin { get; set; }
    public int RoomsMax { get; set; }
    public bool ScrapeIdealistaEnabled { get; set; } = true;
    public bool ScrapeImoVirtualEnabled { get; set; } = true;
    public bool ScrapeImobiliarioEnabled { get; set; } = true;
}
