namespace EstateAggregator.DTOs;

public class AppSettingDto
{
    public List<string> Districts { get; set; } = new();
    public decimal PriceMin { get; set; }
    public decimal PriceMax { get; set; }
    public int RoomsMin { get; set; }
    public int RoomsMax { get; set; }
    public int MaxPagesPerSource { get; set; }
    public bool ScrapeIdealistaEnabled { get; set; }
    public bool ScrapeImoVirtualEnabled { get; set; }
    public bool ScrapeImobiliarioEnabled { get; set; }
    public bool ScrapeCasaSapoEnabled { get; set; }
    public bool ScrapeCaixaImobiliarioEnabled { get; set; }
    public bool ScrapeSantanderEnabled { get; set; }
    public string? AvailabilityText { get; set; }
    public string? SenderName { get; set; }
    public DateTime? LastScrapedAt { get; set; }
}

public class UpdateAppSettingDto
{
    public List<string> Districts { get; set; } = new();
    public decimal PriceMin { get; set; }
    public decimal PriceMax { get; set; }
    public int RoomsMin { get; set; }
    public int RoomsMax { get; set; }
    public int MaxPagesPerSource { get; set; } = 8;
    public bool ScrapeIdealistaEnabled { get; set; } = true;
    public bool ScrapeImoVirtualEnabled { get; set; } = true;
    public bool ScrapeImobiliarioEnabled { get; set; } = true;
    public bool ScrapeCasaSapoEnabled { get; set; } = true;
    public bool ScrapeCaixaImobiliarioEnabled { get; set; } = true;
    public bool ScrapeSantanderEnabled { get; set; } = true;
    public string? AvailabilityText { get; set; }
    public string? SenderName { get; set; }
}
