using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using EstateAggregator.Utilities;
using HtmlAgilityPack;

namespace EstateAggregator.Services.Scrapers;

// Imobiliario.pt (the originally intended target — see the class name/the
// "Imobiliario" AppSetting flag/DB column this stays wired to) turned out to
// be a parked/for-sale domain, not a live listings site. custojusto.pt was
// picked as the real replacement and verified against the live site:
//
//   curl -A "Mozilla/5.0 ..." https://www.custojusto.pt/porto/imobiliario/apartamentos
//
// returns HTTP 200, a server-rendered Next.js page with the same
// __NEXT_DATA__ embedded-JSON pattern ImoVirtualScraper already parses (see
// that file for the general approach). A few things were confirmed live
// before committing to this shape:
//   - "/{district}/imobiliario/apartamentos" is a real, working per-district,
//     apartments-only category page (region=Porto -> category "Apartamentos"
//     only, confirmed via the page's own queryData.structure.category).
//   - The site's own priceGte/priceLte/bedroomNrGte/bedroomNrLte query
//     params (visible in queryData.structure) do NOT actually filter the
//     server-rendered result set — they're echoed into the Next.js router's
//     query object but never reach the data fetched server-side, so (like
//     CasaSapoScraper) this fetches broadly per district and applies the
//     user's price/rooms filters in code instead of trusting query params.
//   - Pagination *does* work, but via "?o={page}" (not "?page=" or the
///    "page" field seen in queryData.structure, both of which were dead
//     ends — "o=2"/"o=3" returned genuinely different, non-overlapping
//     result sets). Past the last page the site 307-redirects back to page
//     1 rather than returning an empty page, so this tracks seen listing
//     IDs per district and stops once a page yields nothing new, rather
//     than trusting HTTP status (HttpClient follows the redirect
//     transparently, so the 307 itself is never visible here).
//   - The apartamentos category mixes for-sale ("sell") and rental ("let")
//     listings in the same feed; only "sell" is kept, to match every other
//     scraper's buy-focused results.
//
// See ImoVirtualScraper.cs for the rationale on why Idealista isn't
// implemented this way (DataDome bot protection).
public class ImobiliarioScraper : IPropertyScraper
{
    private readonly HttpClient _httpClient;
    private readonly EstateDbContext _db;
    private readonly DeduplicationService _dedupService;
    private readonly ILogger<ImobiliarioScraper> _logger;
    private bool _hasMadeFirstRequest;

    public string Source => "CustoJusto";

    // A smaller/less battle-tested site than CasaSapo or ImoVirtual, so the
    // delay below stays conservative (longer than strictly required by
    // anything observed so far); page count is user-controlled via
    // AppSetting.MaxPagesPerSource.
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(1300);

    private static readonly Regex NextDataRegex = new(
        "<script id=\"__NEXT_DATA__\"[^>]*>(.*?)</script>", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex RoomsRegex = new(@"[Tt](\d+)", RegexOptions.Compiled);
    private static readonly Regex SizeRegex = new(@"(\d+(?:[.,]\d+)?)\s*m", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // custojusto's district-root pages ("/{slug}/imobiliario/apartamentos")
    // are real administrative districts, not municipalities — e.g. Cascais
    // is a sub-area under "/lisboa/cascais", not its own root path (verified
    // live: "/cascais/imobiliario/apartamentos" 308-redirects away). So
    // unlike ImoVirtualScraper's alias table, this one intentionally omits
    // municipality-level entries. Only lisboa/porto/braga/coimbra/faro/
    // setubal/evora were live-verified; the rest follow the same confirmed
    // lowercase-unaccented pattern.
    private static readonly Dictionary<string, string> DistrictSlugAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Lisbon"] = "lisboa",
        ["Lisboa"] = "lisboa",
        ["Porto"] = "porto",
        ["Oporto"] = "porto",
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

    public ImobiliarioScraper(HttpClient httpClient, EstateDbContext db, DeduplicationService dedupService, ILogger<ImobiliarioScraper> logger)
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

        _hasMadeFirstRequest = false;
        foreach (var district in districts)
        {
            await ScrapeDistrictAsync(district, settings, report, cancellationToken);
        }

        report.EndTime = DateTime.UtcNow;
        return report;
    }

    private async Task ScrapeDistrictAsync(string district, AppSetting settings, ScraperReportDto report, CancellationToken cancellationToken)
    {
        var slug = ResolveDistrictSlug(district);

        // Past the last real page, custojusto 307-redirects back to page 1
        // instead of returning an empty result — HttpClient follows that
        // transparently, so a repeat of page 1's listing IDs (rather than an
        // empty list) is the actual end-of-results signal.
        var seenIds = new HashSet<string>();

        for (var page = 1; page <= settings.MaxPagesPerSource; page++)
        {
            if (_hasMadeFirstRequest)
                await Task.Delay(DelayBetweenRequests, cancellationToken);
            _hasMadeFirstRequest = true;

            var url = BuildSearchUrl(slug, page);
            CustoJustoNextData? data;

            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("CustoJusto returned {Status} for {District} page {Page}", response.StatusCode, district, page);
                    report.HasErrors = true;
                    report.Errors.Add($"{district}: HTTP {(int)response.StatusCode}");
                    return;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                data = ParseNextData(html);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CustoJusto request/parse failed for {District} page {Page}", district, page);
                report.HasErrors = true;
                report.Errors.Add($"{district}: {ex.Message}");
                return;
            }

            var pageItems = data?.Props?.PageProps?.ListItems ?? new List<CustoJustoListItem>();
            if (pageItems.Count == 0)
                return;

            var newItems = pageItems.Where(i => !string.IsNullOrEmpty(i.ListID) && seenIds.Add(i.ListID!)).ToList();
            if (newItems.Count == 0)
                return; // wrapped back to page 1 — no more results for this district

            var saleItems = newItems.Where(i => i.Type == "sell").ToList();
            report.PropertiesFound += saleItems.Count;

            foreach (var item in saleItems)
            {
                var property = MapToProperty(item, district);

                if (property.Price.HasValue && (property.Price < settings.PriceMin || property.Price > settings.PriceMax))
                    continue;

                if (property.Beds.HasValue && (property.Beds < settings.RoomsMin || property.Beds > settings.RoomsMax))
                    continue;

                var outcome = await _dedupService.ProcessAsync(_db, property);
                switch (outcome)
                {
                    case DedupOutcome.Added: report.PropertiesAdded++; break;
                    case DedupOutcome.Updated: report.PropertiesUpdated++; break;
                    case DedupOutcome.Linked: report.PropertiesLinked++; break;
                    case DedupOutcome.Skipped: report.PropertiesSkipped++; break;
                }
            }

            // Saved per page, not once at the end — dedup-by-hash checks
            // against already-processed listings query the database, not
            // pending unsaved inserts in the change tracker.
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string BuildSearchUrl(string districtSlug, int page)
    {
        var baseUrl = $"https://www.custojusto.pt/{districtSlug}/imobiliario/apartamentos";
        return page <= 1 ? baseUrl : $"{baseUrl}?o={page}";
    }

    private static CustoJustoNextData? ParseNextData(string html)
    {
        var match = NextDataRegex.Match(html);
        if (!match.Success)
            throw new InvalidOperationException("__NEXT_DATA__ script tag not found — page structure may have changed");

        return JsonSerializer.Deserialize<CustoJustoNextData>(match.Groups[1].Value, JsonOptions);
    }

    private Property MapToProperty(CustoJustoListItem item, string fallbackDistrict)
    {
        var district = item.LocationNames?.District ?? fallbackDistrict;
        var neighborhood = item.LocationNames?.Parish ?? item.LocationNames?.County;
        var locationString = string.IsNullOrEmpty(neighborhood) ? district : $"{district}, {neighborhood}";

        var description = HtmlEntity.DeEntitize(item.Body ?? string.Empty) ?? string.Empty;
        var (orientation, orientationSource) = OrientationExtractor.Extract(description);
        var openPlanKitchen = OpenPlanKitchenExtractor.Extract(description);
        var constructionStatus = ConstructionStatusExtractor.Extract(description);
        var elevator = ElevatorExtractor.Extract(description);
        var parking = ParkingExtractor.Extract(description);
        var beds = ParseRooms(item.Params?.Rooms);
        var size = ParseSize(item.Params?.Size);
        var price = item.Price;

        var url = ResolveUrl(item.Url, item.ListID);
        var photos = string.IsNullOrEmpty(item.ImageFullURL) ? Array.Empty<string>() : new[] { item.ImageFullURL };

        return new Property
        {
            Url = url,
            Source = Source,
            Price = price,
            LocationString = locationString,
            Beds = beds,
            Baths = null,
            SizeM2 = size,
            Description = description,
            SunOrientation = orientation,
            OrientationSource = orientationSource,
            OpenPlanKitchen = openPlanKitchen,
            ConstructionStatus = constructionStatus,
            Elevator = elevator,
            Parking = parking,
            PhotosJson = JsonSerializer.Serialize(photos),
            SourcePropertyId = item.ListID,
            DedupHash = DedupHashGenerator.Compute(locationString, price ?? 0, beds),
            // See CustoJustoListItem.Name's comment — free at search level,
            // but the phone is gated behind a client-side reveal action this
            // scraper doesn't replicate, so AgentPhone stays unset.
            AgentName = item.Name
        };
    }

    private static string ResolveUrl(string? relativeOrAbsoluteUrl, string? listId)
    {
        if (!string.IsNullOrEmpty(relativeOrAbsoluteUrl))
        {
            return relativeOrAbsoluteUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? relativeOrAbsoluteUrl
                : "https://www.custojusto.pt" + relativeOrAbsoluteUrl;
        }

        // Fallback so a listing with an unexpectedly missing url still gets
        // a plausible, source-identifiable value rather than an empty
        // string (Url has a unique DB constraint).
        return $"https://www.custojusto.pt/anuncio/{listId}";
    }

    private static int? ParseRooms(string? roomsText)
    {
        if (string.IsNullOrEmpty(roomsText))
            return null;

        var match = RoomsRegex.Match(roomsText);
        return match.Success && int.TryParse(match.Groups[1].Value, out var rooms) ? rooms : null;
    }

    private static decimal? ParseSize(string? sizeText)
    {
        if (string.IsNullOrEmpty(sizeText))
            return null;

        var match = SizeRegex.Match(sizeText);
        if (!match.Success)
            return null;

        var raw = match.Groups[1].Value.Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var size) ? size : null;
    }

    private static string ResolveDistrictSlug(string district)
    {
        var trimmed = district.Trim();
        return DistrictSlugAliases.TryGetValue(trimmed, out var slug)
            ? slug
            : DedupHashGenerator.NormalizeLocation(trimmed).Replace(' ', '-');
    }
}
