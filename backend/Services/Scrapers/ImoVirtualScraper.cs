using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using EstateAggregator.Utilities;
using HtmlAgilityPack;
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

    // Observed live: the search response's own Images list never exceeds 3
    // preview thumbnails regardless of how many photos the listing actually
    // has (a typical detail page carries 12+) — see FetchDetailAsync.
    private const int SearchResultPhotoCap = 3;

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

        // Verified live: ImoVirtual's own "porto" district search occasionally
        // mixes in listings from neighboring districts (found real Braga and
        // Aveiro results — border municipalities like Vizela, Famalicão,
        // Santa Maria da Feira — inside a search scoped to Porto only). Every
        // other scraper either trusts a similarly district-scoped site URL
        // with no leaks observed, or already has this same code-side check
        // (CaixaImobiliario/Santander, whose site-side filtering was known
        // unreliable from the start) — this gives ImoVirtual the same safety
        // net now that it's shown to need one too.
        var normalizedDistrict = DedupHashGenerator.NormalizeLocation(DistrictAliases.ToPortuguese(district));

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
            var existingByUrl = await _db.Properties
                .Where(p => pageUrls.Contains(p.Url))
                .Select(p => new { p.Url, p.AgentPhone, p.PhotosJson, p.Description })
                .ToDictionaryAsync(p => p.Url, cancellationToken);

            foreach (var item in items)
            {
                var property = MapToProperty(item);

                if (!DedupHashGenerator.NormalizeLocation(property.LocationString).Contains(normalizedDistrict))
                    continue;

                // The detail page costs an extra HTTP request, so it's only
                // fetched when there's something real to gain: no phone yet,
                // a photo count still stuck at the search-result cap (3), or
                // no full description (search results only carry a
                // ShortDescription that's genuinely truncated mid-sentence).
                // Genuinely new properties always qualify. A large backlog of
                // already-tracked properties predates this enrichment
                // existing at all — this also backfills those on the next
                // scrape rather than only ever enriching brand-new ones.
                existingByUrl.TryGetValue(property.Url, out var existing);
                var needsDetail = existing == null
                    || string.IsNullOrEmpty(existing.AgentPhone)
                    || CountPhotos(existing.PhotosJson) <= SearchResultPhotoCap
                    || string.IsNullOrEmpty(existing.Description);

                if (needsDetail && !string.IsNullOrEmpty(item.Slug))
                {
                    await Task.Delay(DelayBetweenRequests, cancellationToken);
                    var detail = await FetchDetailAsync(item.Slug, cancellationToken);
                    if (!string.IsNullOrEmpty(detail.Phone))
                        property.AgentPhone = detail.Phone;
                    if (detail.Photos is { Count: > 0 })
                        property.PhotosJson = JsonSerializer.Serialize(detail.Photos);
                    if (!string.IsNullOrEmpty(detail.Description))
                        property.Description = DescriptionCleaner.Clean(detail.Description);
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

    // ImoVirtual's own "roomsNumber" facet counts total divisions (bedrooms
    // plus the living room as one more "room"), not bedrooms alone —
    // verified live across ~500 cross-checked listings: description text
    // says "T1" (1 bedroom) for listings whose own roomsNumber is "TWO",
    // consistently one higher, across 92% of samples. So "ONE" is actually
    // a T0 studio, "TWO" is T1, ..., "FIVE" is T4, and "MORE" is T5+ — a
    // desired bedroom count of N maps to the label at position N+1, not N.
    // See MapRooms for the corresponding fix on the read side (what gets
    // displayed as Beds for an already-fetched listing).
    private static List<string> BuildRoomsFilter(int roomsMin, int roomsMax)
    {
        var labels = new[] { "ONE", "TWO", "THREE", "FOUR", "FIVE" }; // T0, T1, T2, T3, T4
        var min = Math.Clamp((roomsMin <= 0 ? 0 : roomsMin) + 1, 1, 5);
        var max = Math.Clamp((roomsMax <= 0 ? 4 : roomsMax) + 1, 1, 5);
        if (min > max) (min, max) = (max, min);

        var result = new List<string>();
        for (var i = min; i <= max; i++)
            result.Add(labels[i - 1]);

        if (roomsMax >= 5)
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
        var furnished = FurnishedExtractor.Extract(description);
        var airConditioning = AirConditioningExtractor.Extract(description);
        var balcony = BalconyExtractor.Extract(description);
        var renovated = RenovatedExtractor.Extract(description);
        var storage = StorageExtractor.Extract(description);
        var waterView = WaterViewExtractor.Extract(description);
        var nearMetro = NearMetroExtractor.Extract(description);
        var hasUsageLicense = UsageLicenseExtractor.Extract(description);
        var energyRating = EnergyRatingExtractor.Extract(description);
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
            // No freguesia-level field exists in ImoVirtual's address JSON
            // (just City/Province), so Freguesia stays null here.
            Distrito = province,
            Concelho = city,
            Beds = beds,
            Baths = null,
            SizeM2 = item.AreaInSquareMeters,
            Description = DescriptionCleaner.Clean(description),
            SunOrientation = orientation,
            OrientationSource = orientationSource,
            OpenPlanKitchen = openPlanKitchen,
            ConstructionStatus = constructionStatus,
            Elevator = elevator,
            Parking = parking,
            Furnished = furnished,
            AirConditioning = airConditioning,
            Balcony = balcony,
            Renovated = renovated,
            Storage = storage,
            WaterView = waterView,
            NearMetro = nearMetro,
            HasUsageLicense = hasUsageLicense,
            EnergyRating = energyRating,
            PhotosJson = JsonSerializer.Serialize(photos),
            SourcePropertyId = item.Id.ToString(CultureInfo.InvariantCulture),
            DedupHash = DedupHashGenerator.Compute(locationString, price ?? 0, beds, item.AreaInSquareMeters),
            // Free at search-result level — private-seller name, or the
            // agency name when it's an agency listing. AgentPhone/full
            // Photos/full Description are set separately (see
            // ScrapeDistrictAsync/FetchDetailAsync) only when needed.
            AgentName = item.AdvertOwner?.Name ?? item.Agency?.Name
        };
    }

    private record ImoVirtualDetailResult(string? Phone, List<string>? Photos, string? Description);

    // None of resolvedPhone, the full photo gallery, or the full description
    // are present anywhere in the search response (advertOwner.contacts is
    // always an empty array there, Images is capped to a handful of preview
    // thumbnails, ShortDescription is genuinely truncated mid-sentence) —
    // all three only live on the individual listing's own page, so one
    // request here covers all three instead of three separate ones.
    // Verified live: 401/403/layout-changed all fail closed (return an
    // all-null result) rather than throwing, since missing detail shouldn't
    // abort an otherwise-successful scrape of everything else on the page.
    private async Task<ImoVirtualDetailResult> FetchDetailAsync(string slug, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync($"https://www.imovirtual.com/pt/anuncio/{slug}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ImoVirtual detail page returned {Status} for {Slug}", response.StatusCode, slug);
                return new ImoVirtualDetailResult(null, null, null);
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var match = NextDataRegex.Match(html);
            if (!match.Success)
                return new ImoVirtualDetailResult(null, null, null);

            var detail = JsonSerializer.Deserialize<ImoVirtualDetailNextData>(match.Groups[1].Value, JsonOptions);
            var pageProps = detail?.Props?.PageProps;

            var phone = pageProps?.UnifiedAd?.ResolvedPhone;
            var photos = pageProps?.Ad?.Images?
                .Select(i => i.Medium)
                .Where(u => !string.IsNullOrEmpty(u))
                .Select(u => u!)
                .ToList();
            var rawDescription = pageProps?.UnifiedAd?.Description;
            var description = string.IsNullOrEmpty(rawDescription) ? null : StripDescriptionHtml(rawDescription);

            return new ImoVirtualDetailResult(phone, photos, description);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImoVirtual detail lookup failed for {Slug}", slug);
            return new ImoVirtualDetailResult(null, null, null);
        }
    }

    // The detail page's description is an HTML fragment ("<p>...</p><p>...
    // </p>"), unlike every other field here — <p>/<br> boundaries are turned
    // into newlines first so paragraphs don't get mashed together, then tags
    // are stripped and entities decoded via HtmlAgilityPack (same library
    // every other scraper already uses for this).
    private static string StripDescriptionHtml(string html)
    {
        var withBreaks = Regex.Replace(html, @"</p>|<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        var doc = new HtmlDocument();
        doc.LoadHtml(withBreaks);
        var text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText) ?? string.Empty;
        return Regex.Replace(text.Trim(), @"\n{3,}", "\n\n");
    }

    private static int CountPhotos(string? photosJson)
    {
        if (string.IsNullOrEmpty(photosJson))
            return 0;

        try
        {
            return JsonSerializer.Deserialize<List<string>>(photosJson)?.Count ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    // See BuildRoomsFilter's comment: ImoVirtual's roomsNumber counts total
    // divisions (bedrooms + the living room), not bedrooms alone — a
    // listing whose own roomsNumber is "TWO" is a T1 (1 bedroom), verified
    // live and cross-checked against ~500 listings' own description text.
    // "MORE" has no exact bedroom count (it's an open-ended "5 or above"
    // bucket) — 5 is used as an honest floor, not a precise value.
    private static int? MapRooms(string? roomsNumber) => roomsNumber switch
    {
        "ONE" => 0,
        "TWO" => 1,
        "THREE" => 2,
        "FOUR" => 3,
        "FIVE" => 4,
        "MORE" => 5,
        _ => null
    };
}
