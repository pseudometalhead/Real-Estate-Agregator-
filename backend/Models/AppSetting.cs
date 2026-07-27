namespace EstateAggregator.Models;

public class AppSetting
{
    public int Id { get; set; }

    public string DistrictsJson { get; set; } = "[]";
    public decimal PriceMin { get; set; }
    public decimal PriceMax { get; set; }
    public int RoomsMin { get; set; }
    public int RoomsMax { get; set; }

    // How many pages each scraper will fetch per district/source before
    // stopping, user-controlled instead of a silent hardcoded ceiling — see
    // the per-scraper MaxPagesPerDistrict/MaxPages comments this replaced
    // for why each site had a different conservative default (5-10). 8 is
    // roughly the middle of those, not a claim that it's the "right" cap.
    public int MaxPagesPerSource { get; set; } = 50;

    public bool ScrapeIdealistaEnabled { get; set; } = true;
    public bool ScrapeImoVirtualEnabled { get; set; } = true;
    public bool ScrapeImobiliarioEnabled { get; set; } = true;
    public bool ScrapeCasaSapoEnabled { get; set; } = true;
    public bool ScrapeCaixaImobiliarioEnabled { get; set; } = true;
    public bool ScrapeSantanderEnabled { get; set; } = true;

    public string LogFilePath { get; set; } = "/app/logs/estate-aggregator.log";

    // Free text describing when the user is available for a viewing (e.g.
    // "Dias de semana após as 18h, ou fins-de-semana"), reused verbatim in
    // every drafted inquiry message — see messageTemplates.js draftInquiry().
    public string? AvailabilityText { get; set; }

    // Signs every drafted inquiry message ("Cumprimentos, {SenderName}") —
    // kept as user-editable DB data rather than hardcoded in source, same
    // reasoning as AvailabilityText: this file is tracked in git.
    public string? SenderName { get; set; }

    public DateTime? LastScrapedAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
