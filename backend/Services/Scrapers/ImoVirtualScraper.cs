using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using EstateAggregator.Utilities;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Services.Scrapers;

// Real integration, verified against the live site: ImoVirtual.com is a
// Next.js app that embeds all search-result data as structured JSON in a
// <script id="__NEXT_DATA__"> tag, so this parses that JSON directly rather
// than scraping rendered HTML with CSS selectors (which the original spec
// assumed, and which would be far more brittle).
//
// Idealista.pt is NOT implemented this way: it's behind DataDome bot
// protection (returns 403 to plain HTTP requests) and requires either a
// real browser or Idealista's business partner API. See IdealistaScraper.cs.
//
// Imobiliario.pt (the class name/AppSetting flag ImobiliarioScraper.cs
// stays wired to) turned out to be a parked/for-sale domain, not a live
// listings site — that scraper now targets custojusto.pt instead, using
// this same __NEXT_DATA__ approach. See ImobiliarioScraper.cs.
public class ImoVirtualScraper : IPropertyScraper
{
    private readonly HttpClient _httpClient;
    private readonly EstateDbContext _db;
    private readonly DeduplicationService _dedupService;
    private readonly ILogger<ImoVirtualScraper> _logger;

    public string Source => "ImoVirtual";

    // Personal-use, twice-daily scraper — capped per district (via the
    // user-controlled AppSetting.MaxPagesPerSource) so a single run stays a
    // handful of requests, not a crawl of every matching listing (a broad
    // Lisboa search alone can span 100+ pages).
    private const int PageSize = 36;
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(900);

    private static readonly Regex NextDataRegex = new(
        "<script id=\"__NEXT_DATA__\"[^>]*>(.*?)</script>", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Best-effort English/Portuguese alias -> site URL slug. Falls back to a
    // generic normalize-and-hyphenate for districts not listed here.
    private static readonly Dictionary<string, string> DistrictSlugAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Lisbon"] = "lisboa",
        ["Lisboa"] = "lisboa",
        ["Porto"] = "porto",
        ["Oporto"] = "porto",
        ["Cascais"] = "cascais",
        ["Braga"] = "braga",
        ["Coimbra"] = "coimbra",
        ["Faro"] = "faro",
        ["Setubal"] = "setubal",
        ["Setúbal"] = "setubal",
        ["Aveiro"] = "aveiro",
        ["Leiria"] = "leiria",
        ["Santarem"] = "santarem",
        ["Santarém"] = "santarem",
        ["Viseu"] = "viseu",
        ["Evora"] = "evora",
        ["Évora"] = "evora",
        ["Beja"] = "beja",
        ["Guarda"] = "guarda",
        ["Portalegre"] = "portalegre",
        ["Braganca"] = "braganca",
        ["Bragança"] = "braganca",
    };

    public ImoVirtualScraper(HttpClient httpClient, EstateDbContext db, DeduplicationService dedupService, ILogger<ImoVirtualScraper> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _dedupService = dedupService;
        _logger = logger;
    }

    public async Task<ScraperReportDto> ScrapeAsync(AppSetting settings, CancellationToken cancellationToken = default)
    {
        var report = new ScraperReportDto { Source = Source, StartTime = DateTime.UtcNow };

        List<string> districts;
        try
        {
            districts = JsonSerializer.Deserialize<List<string>>(settings.DistrictsJson) ?? new List<string>();
        }
        catch (JsonException ex)
        {
            report.HasErrors = true;
            report.Errors.Add($"Invalid DistrictsJson: {ex.Message}");
            report.EndTime = DateTime.UtcNow;
            return report;
        }

        var roomsFilter = BuildRoomsFilter(settings.RoomsMin, settings.RoomsMax);

        foreach (var district in districts)
        {
            await ScrapeDistrictAsync(district, settings, roomsFilter, report, cancellationToken);
        }

        report.EndTime = DateTime.UtcNow;
        return report;
    }

    private async Task ScrapeDistrictAsync(
        string district, AppSetting settings, List<string> roomsFilter, ScraperReportDto report, CancellationToken cancellationToken)
    {
        var slug = ResolveDistrictSlug(district);

        for (var page = 1; page <= settings.MaxPagesPerSource; page++)
        {
            if (page > 1)
                await Task.Delay(DelayBetweenRequests, cancellationToken);

            var url = BuildSearchUrl(slug, settings.PriceMin, settings.PriceMax, roomsFilter, page);
            ImoVirtualNextData? data;

            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ImoVirtual returned {Status} for {District} page {Page}", response.StatusCode, district, page);
                    report.HasErrors = true;
                    report.Errors.Add($"{district}: HTTP {(int)response.StatusCode}");
                    return;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                data = ParseNextData(html);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImoVirtual request/parse failed for {District} page {Page}", district, page);
                report.HasErrors = true;
                report.Errors.Add($"{district}: {ex.Message}");
                return;
            }

            var items = data?.Props?.PageProps?.Data?.SearchAds?.Items ?? new List<ImoVirtualItem>();
            if (items.Count == 0)
                return;

            report.PropertiesFound += items.Count;

            // Batched once per page rather than one query per item — an
            // earlier version checked existence per-item in the loop below
            // and it measurably slowed a full run down (269 properties ->
            // 269 extra round trips) even though most pages fetch zero
            // phones, since the check itself still runs for everything.
            var pageUrls = items
                .Where(i => !string.IsNullOrEmpty(i.Slug))
                .Select(i => $"https://www.imovirtual.com/pt/anuncio/{i.Slug}")
                .ToList();
            var existingPhonesByUrl = await _db.Properties
                .Where(p => pageUrls.Contains(p.Url))
                .Select(p => new { p.Url, p.AgentPhone })
                .ToDictionaryAsync(p => p.Url, p => p.AgentPhone, cancellationToken);

            foreach (var item in items)
            {
                var property = MapToProperty(item);

                // The phone number costs an extra HTTP request (the detail
                // page — see FetchPhoneAsync), so it's skipped for anything
                // that's both already tracked AND already has a phone on
                // file. Genuinely new properties get it (as before), but so
                // do already-tracked ones still missing a phone — a large
                // backlog of these existed simply because this fetch didn't
                // exist yet when they were first scraped, and without this
                // they'd never get backfilled on any later re-scrape either.
                var alreadyHasPhone = existingPhonesByUrl.TryGetValue(property.Url, out var existingPhone)
                    && !string.IsNullOrEmpty(existingPhone);
                if (!alreadyHasPhone && !string.IsNullOrEmpty(item.Slug))
                {
                    await Task.Delay(DelayBetweenRequests, cancellationToken);
                    property.AgentPhone = await FetchPhoneAsync(item.Slug, cancellationToken);
                }

                var outcome = await _dedupService.ProcessAsync(_db, property);
                switch (outcome)
                {
                    case DedupOutcome.Added: report.PropertiesAdded++; break;
                    case DedupOutcome.Updated: report.PropertiesUpdated++; break;
                    case DedupOutcome.Linked: report.PropertiesLinked++; break;
                    case DedupOutcome.Skipped: report.PropertiesSkipped++; break;
                }
            }

            // Saved per page (rather than once at the end of the whole run)
            // so dedup-by-hash checks against already-processed listings
            // actually see them — EF Core's LINQ queries hit the database,
            // not pending unsaved inserts in the change tracker.
            await _db.SaveChangesAsync(cancellationToken);

            var totalPages = data?.Props?.PageProps?.Data?.SearchAds?.Pagination?.TotalPages ?? page;
            if (page >= totalPages)
                return;
        }
    }

    private static List<string> BuildRoomsFilter(int roomsMin, int roomsMax)
    {
        var labels = new[] { "ONE", "TWO", "THREE", "FOUR", "FIVE" };
        var min = Math.Clamp(roomsMin <= 0 ? 1 : roomsMin, 1, 5);
        var max = Math.Clamp(roomsMax <= 0 ? 5 : roomsMax, 1, 5);
        if (min > max) (min, max) = (max, min);

        var result = new List<string>();
        for (var i = min; i <= max; i++)
            result.Add(labels[i - 1]);

        if (roomsMax >= 6)
            result.Add("MORE");

        return result;
    }

    private static string ResolveDistrictSlug(string district)
    {
        var trimmed = district.Trim();
        return DistrictSlugAliases.TryGetValue(trimmed, out var slug)
            ? slug
            : DedupHashGenerator.NormalizeLocation(trimmed).Replace(' ', '-');
    }

    private static string BuildSearchUrl(string districtSlug, decimal priceMin, decimal priceMax, List<string> rooms, int page)
    {
        var roomsParam = rooms.Count > 0 ? $"&roomsNumber=%5B{string.Join("%2C", rooms)}%5D" : string.Empty;
        return "https://www.imovirtual.com/pt/resultados/comprar/apartamento/" + districtSlug +
               $"?priceMin={(int)priceMin}&priceMax={(int)priceMax}{roomsParam}&page={page}&limit={PageSize}";
    }

    private static ImoVirtualNextData? ParseNextData(string html)
    {
        var match = NextDataRegex.Match(html);
        if (!match.Success)
            throw new InvalidOperationException("__NEXT_DATA__ script tag not found — page structure may have changed");

        return JsonSerializer.Deserialize<ImoVirtualNextData>(match.Groups[1].Value, JsonOptions);
    }

    private Property MapToProperty(ImoVirtualItem item)
    {
        var province = item.Location?.Address?.Province?.Name;
        var city = item.Location?.Address?.City?.Name;
        var locationString = !string.IsNullOrEmpty(province) && !string.IsNullOrEmpty(city)
            ? $"{province}, {city}"
            : city ?? province ?? "Unknown";

        var description = item.ShortDescription ?? string.Empty;
        var (orientation, orientationSource) = OrientationExtractor.Extract(description);
        var openPlanKitchen = OpenPlanKitchenExtractor.Extract(description);
        var constructionStatus = ConstructionStatusExtractor.Extract(description);
        var elevator = ElevatorExtractor.Extract(description);
        var parking = ParkingExtractor.Extract(description);
        var beds = MapRooms(item.RoomsNumber);
        var price = item.TotalPrice?.Value;

        var photos = item.Images?
            .Select(i => i.Medium)
            .Where(u => !string.IsNullOrEmpty(u))
            .ToList() ?? new List<string?>();

        return new Property
        {
            Url = $"https://www.imovirtual.com/pt/anuncio/{item.Slug}",
            Source = Source,
            Price = price,
            LocationString = locationString,
            Beds = beds,
            Baths = null,
            SizeM2 = item.AreaInSquareMeters,
            Description = description,
            SunOrientation = orientation,
            OrientationSource = orientationSource,
            OpenPlanKitchen = openPlanKitchen,
            ConstructionStatus = constructionStatus,
            Elevator = elevator,
            Parking = parking,
            PhotosJson = JsonSerializer.Serialize(photos),
            SourcePropertyId = item.Id.ToString(CultureInfo.InvariantCulture),
            DedupHash = DedupHashGenerator.Compute(locationString, price ?? 0, beds),
            // Free at search-result level — private-seller name, or the
            // agency name when it's an agency listing. AgentPhone is set
            // separately (see ScrapeDistrictAsync/FetchPhoneAsync) only for
            // properties being added for the first time.
            AgentName = item.AdvertOwner?.Name ?? item.Agency?.Name
        };
    }

    // Only called for genuinely new properties — the phone number isn't
    // present anywhere in the search response (advertOwner.contacts is
    // always an empty array there), only on the individual listing's own
    // page. Verified live: 401/403/layout-changed all fail closed (return
    // null) rather than throwing, since a missing phone shouldn't abort an
    // otherwise-successful scrape of everything else on the page.
    private async Task<string?> FetchPhoneAsync(string slug, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync($"https://www.imovirtual.com/pt/anuncio/{slug}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ImoVirtual detail page returned {Status} for {Slug}", response.StatusCode, slug);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var match = NextDataRegex.Match(html);
            if (!match.Success)
                return null;

            var detail = JsonSerializer.Deserialize<ImoVirtualDetailNextData>(match.Groups[1].Value, JsonOptions);
            return detail?.Props?.PageProps?.UnifiedAd?.ResolvedPhone;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImoVirtual phone lookup failed for {Slug}", slug);
            return null;
        }
    }

    private static int? MapRooms(string? roomsNumber) => roomsNumber switch
    {
        "ONE" => 1,
        "TWO" => 2,
        "THREE" => 3,
        "FOUR" => 4,
        "FIVE" => 5,
        "MORE" => 6,
        _ => null
    };
}
