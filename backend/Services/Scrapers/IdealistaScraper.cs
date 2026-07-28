using System.Net;
using System.Text.Json;
using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using EstateAggregator.Utilities;

namespace EstateAggregator.Services.Scrapers;

// Real integration against a third-party wrapper API, NOT an official
// Idealista API: https://rapidapi.com/kiwimaker/api/idealista-real-estate
// (backed by apidea.es). idealista.pt itself sits behind DataDome bot
// protection and returns 403 to every plain HTTP request, including its
// own homepage — this app never attempts to scrape or bypass that
// directly. See docs/idealista-integration-plan.md for the alternatives
// considered (official partner API, RSS, manual import via
// POST /api/properties/import) and why this one was picked: fully
// documented (OpenAPI 3.1 spec at https://apidea.es/openapi.json — every
// field/param name below was checked against the raw spec, not guessed),
// covers Portugal (`country=pt`), and returns lat/lng directly, so these
// listings need no separate geocoding pass.
//
// Requires a RapidAPI key in the IDEALISTA_RAPIDAPI_KEY env var (see
// .env). Without one, this returns the same honest "not
// configured" report the original stub did.
//
// Verified against a real key and real Porto results (75 listings added,
// zero errors). Live testing caught three things the spec alone couldn't
// have: (1) `locale` defaults to "es" upstream — without &locale=pt,
// descriptions/addresses come back in Spanish, silently breaking
// OrientationExtractor/OpenPlanKitchenExtractor, both Portuguese-only; (2)
// autocomplete("Porto") returns the municipality before the district, so
// ResolveLocationIdAsync explicitly prefers a "Distrito" match; (3) this
// API's own "district" field is actually neighborhood-level ("Baixa") and
// "province" is the district-level field ("Porto") — the reverse of what
// the bare field names suggest, see MapToProperty.
public class IdealistaScraper : IPropertyScraper
{
    private readonly HttpClient _httpClient;
    private readonly EstateDbContext _db;
    private readonly DeduplicationService _dedupService;
    private readonly ILogger<IdealistaScraper> _logger;

    public string Source => "Idealista";

    private const string ApiBaseUrl = "https://idealista-real-estate.p.rapidapi.com";
    private const string ApiHost = "idealista-real-estate.p.rapidapi.com";

    // Page count is user-controlled via AppSetting.MaxPagesPerSource —
    // start conservative until real usage against a real plan/quota is
    // observed, and raise it once pricing/limits are known (see the plan doc).
    private const int PageSize = 40; // upstream cap, per the OpenAPI spec

    // The search endpoint's own multimedia is truncated to 1 photo no
    // matter how many actually exist (see IdealistaPropertyDetailResponse's
    // comment) — GET /v1/property/{ad_id} is the only way to get the full
    // gallery, at the cost of one extra API call per listing. A small delay
    // keeps this from bursting RapidAPI's rate limit the way back-to-back
    // requests for every listing on a 40-item page otherwise would.
    private static readonly TimeSpan DelayBetweenDetailRequests = TimeSpan.FromMilliseconds(250);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IdealistaScraper(HttpClient httpClient, EstateDbContext db, DeduplicationService dedupService, ILogger<IdealistaScraper> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _dedupService = dedupService;
        _logger = logger;
    }

    public async Task<ScraperReportDto> ScrapeAsync(AppSetting settings, CancellationToken cancellationToken = default)
    {
        var report = new ScraperReportDto { Source = Source, StartTime = DateTime.UtcNow };

        var apiKey = Environment.GetEnvironmentVariable("IDEALISTA_RAPIDAPI_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("{Source} scraping skipped — IDEALISTA_RAPIDAPI_KEY is not set", Source);
            report.HasErrors = true;
            report.Errors.Add("Not configured — set IDEALISTA_RAPIDAPI_KEY (see .env) to enable this source.");
            report.EndTime = DateTime.UtcNow;
            return report;
        }

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

        var bedroomsFilter = BuildBedroomsFilter(settings.RoomsMin, settings.RoomsMax);

        foreach (var district in districts)
        {
            var locationId = await ResolveLocationIdAsync(district, apiKey, cancellationToken);
            if (locationId == null)
            {
                report.HasErrors = true;
                report.Errors.Add($"{district}: could not resolve a location id");
                continue;
            }

            await ScrapeLocationAsync(district, locationId, settings, bedroomsFilter, apiKey, report, cancellationToken);
        }

        report.EndTime = DateTime.UtcNow;
        return report;
    }

    // /v1/locations/autocomplete has no country-hierarchy browser like
    // Spain's listSpanishRegions — Portuguese location ids (e.g. Porto's
    // "0-EU-PT-..." tag) aren't published anywhere in the spec, so they're
    // resolved by name at request time instead of hardcoded.
    private async Task<string?> ResolveLocationIdAsync(string district, string apiKey, CancellationToken cancellationToken)
    {
        var url = $"{ApiBaseUrl}/v1/locations/autocomplete?query={Uri.EscapeDataString(district)}" +
                   "&country=pt&locale=pt&operation=sale&propertyType=homes";

        try
        {
            var response = await SendAsync(url, apiKey, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Idealista location lookup for {District} returned {Status}", district, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<IdealistaLocationsResponse>(json, JsonOptions);
            var candidates = data?.Locations.Where(l => !string.IsNullOrEmpty(l.LocationId)).ToList()
                ?? new List<IdealistaLocationNode>();

            // Verified live: autocomplete("Porto") returns the municipality
            // ("Porto, Porto", subTypeText="Concelho") before the district
            // ("Porto", subTypeText="Distrito"). Every other scraper in this
            // app treats a configured district like "Porto" as the whole
            // administrative district (CustoJusto/CasaSapo results already
            // span Vila Nova de Gaia, Ermesinde, etc. under "Porto"), so
            // prefer the "Distrito" match here too rather than silently
            // narrowing to just the city.
            return candidates.FirstOrDefault(l => l.SubTypeText == "Distrito")?.LocationId
                ?? candidates.FirstOrDefault()?.LocationId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Idealista location lookup failed for {District}", district);
            return null;
        }
    }

    private async Task ScrapeLocationAsync(
        string district, string locationId, AppSetting settings, string? bedroomsFilter,
        string apiKey, ScraperReportDto report, CancellationToken cancellationToken)
    {
        for (var page = 1; page <= settings.MaxPagesPerSource; page++)
        {
            var url = BuildSearchUrl(locationId, settings, bedroomsFilter, page);
            IdealistaSearchResponse? data;

            try
            {
                var response = await SendAsync(url, apiKey, cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    report.HasErrors = true;
                    report.Errors.Add("HTTP 401 — IDEALISTA_RAPIDAPI_KEY is invalid or expired");
                    return;
                }

                if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Idealista returned 422 for {District} page {Page}: {Body}", district, page, body);
                    report.HasErrors = true;
                    report.Errors.Add($"{district}: HTTP 422 (invalid search parameter — see logs)");
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Idealista returned {Status} for {District} page {Page}", response.StatusCode, district, page);
                    report.HasErrors = true;
                    report.Errors.Add($"{district}: HTTP {(int)response.StatusCode}");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                data = JsonSerializer.Deserialize<IdealistaSearchResponse>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Idealista request/parse failed for {District} page {Page}", district, page);
                report.HasErrors = true;
                report.Errors.Add($"{district}: {ex.Message}");
                return;
            }

            var items = data?.ElementList ?? new List<IdealistaSearchElement>();
            if (items.Count == 0)
                return;

            report.PropertiesFound += items.Count;

            foreach (var item in items)
            {
                var property = MapToProperty(item, district);

                // Belt-and-suspenders on top of the server-side priceFrom/priceTo/
                // bedrooms params, matching every other scraper in this project.
                if (property.Price.HasValue && (property.Price < settings.PriceMin || property.Price > settings.PriceMax))
                    continue;

                if (property.Beds.HasValue && (property.Beds < settings.RoomsMin || property.Beds > settings.RoomsMax))
                    continue;

                await Task.Delay(DelayBetweenDetailRequests, cancellationToken);
                var fullGallery = await FetchAllPhotosAsync(item.PropertyCode, apiKey, cancellationToken);
                if (fullGallery.Count > 0)
                    property.PhotosJson = JsonSerializer.Serialize(fullGallery);

                var outcome = await _dedupService.ProcessAsync(_db, property);
                switch (outcome)
                {
                    case DedupOutcome.Added: report.PropertiesAdded++; break;
                    case DedupOutcome.Updated: report.PropertiesUpdated++; break;
                    case DedupOutcome.Linked: report.PropertiesLinked++; break;
                    case DedupOutcome.Skipped: report.PropertiesSkipped++; break;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            var totalPages = data?.TotalPages ?? page;
            if (page >= totalPages)
                return;
        }
    }

    // Falls back to an empty list (caller keeps the single search-result
    // thumbnail already on the candidate) rather than failing the whole
    // listing on a transient detail-endpoint error — losing the extra
    // gallery photos for one listing isn't worth aborting a page's worth of
    // otherwise-good results over.
    private async Task<List<string>> FetchAllPhotosAsync(string propertyCode, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{ApiBaseUrl}/v1/property/{propertyCode}?country=pt&locale=pt";
            var response = await SendAsync(url, apiKey, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Idealista property detail for {PropertyCode} returned {Status}", propertyCode, response.StatusCode);
                return new List<string>();
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<IdealistaPropertyDetailResponse>(json, JsonOptions);
            return data?.Multimedia?.Images
                .Select(i => i.Url)
                .Where(u => !string.IsNullOrEmpty(u))
                .Select(u => u!)
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Idealista property detail fetch failed for {PropertyCode}", propertyCode);
            return new List<string>();
        }
    }

    // Headers are attached per-request rather than as HttpClient
    // DefaultRequestHeaders, since the client is a shared singleton and
    // mutating shared default headers per call isn't thread-safe.
    private async Task<HttpResponseMessage> SendAsync(string url, string apiKey, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-rapidapi-key", apiKey);
        request.Headers.Add("x-rapidapi-host", ApiHost);
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static string BuildSearchUrl(string locationId, AppSetting settings, string? bedroomsFilter, int page)
    {
        // locale defaults to "es" upstream if omitted — verified live that
        // without &locale=pt, descriptions (and address/district labels)
        // come back in Spanish, which silently breaks OrientationExtractor/
        // OpenPlanKitchenExtractor (both built for Portuguese phrases only).
        var url = $"{ApiBaseUrl}/v1/search?operation=sale&propertyType=homes&country=pt&locale=pt" +
                   $"&locationIds={Uri.EscapeDataString(locationId)}" +
                   $"&numPage={page}&maxItems={PageSize}&order=publicationDate&sort=desc";

        if (settings.PriceMin > 0)
            url += $"&priceFrom={(long)settings.PriceMin}";
        if (settings.PriceMax > 0)
            url += $"&priceTo={(long)settings.PriceMax}";
        if (!string.IsNullOrEmpty(bedroomsFilter))
            url += $"&bedrooms={bedroomsFilter}";

        return url;
    }

    // roomsMin<=0 && roomsMax>=6 means "no meaningful filter" (the same
    // convention every other scraper's own rooms filter uses) — omit the
    // param entirely rather than send a list capped at 6, which would
    // silently exclude larger homes the in-code safety-net filter above
    // would otherwise still allow through.
    private static string? BuildBedroomsFilter(int roomsMin, int roomsMax)
    {
        if (roomsMin <= 0 && roomsMax >= 6)
            return null;

        var min = Math.Clamp(roomsMin, 0, 6);
        var max = Math.Clamp(roomsMax <= 0 ? 6 : roomsMax, 0, 6);
        if (min > max) (min, max) = (max, min);

        return string.Join(',', Enumerable.Range(min, max - min + 1));
    }

    private Property MapToProperty(IdealistaSearchElement item, string fallbackDistrict)
    {
        // Verified live against real Porto results: this API's "province"
        // field is the broad area this app calls a "district" (e.g.
        // "Porto"), while its own "district" field is actually a specific
        // neighborhood/civil-parish name (e.g. "Baixa") — the opposite of
        // what the bare field names suggest. "Municipality" is a fallback
        // for results with no district/neighborhood (e.g. "Moreira").
        var district = item.Province ?? fallbackDistrict;
        var neighborhood = item.District ?? item.Neighborhood ?? item.Municipality;
        var locationString = string.IsNullOrEmpty(neighborhood) ? district : $"{district}, {neighborhood}";
        // Concelho/Freguesia populated separately from the same fields above
        // — Municipality is concelho-level, District/Neighborhood is
        // freguesia-level, and a single result can carry both at once even
        // though locationString above only ever shows one of them.
        var concelho = item.Municipality;
        var freguesia = item.District ?? item.Neighborhood;

        var description = item.Description ?? string.Empty;
        var (orientation, orientationSource) = OrientationExtractor.Extract(description);
        var openPlanKitchen = OpenPlanKitchenExtractor.Extract(description);
        var constructionStatus = ConstructionStatusExtractor.Extract(description);
        var elevator = ElevatorExtractor.Extract(description);
        var parking = ParkingExtractor.Extract(description);
        var furnished = FurnishedExtractor.Extract(description);
        // Prefer the API's own structured flag (only ever sent as true, when
        // stated) over the regex fallback, but still fall back to it when the
        // API is silent on a given amenity.
        var airConditioning = item.Features?.HasAirConditioning == true
            ? true
            : AirConditioningExtractor.Extract(description);
        var balcony = item.Features?.HasTerrace == true
            ? true
            : BalconyExtractor.Extract(description);
        var renovated = RenovatedExtractor.Extract(description);
        var storage = StorageExtractor.Extract(description);
        var waterView = WaterViewExtractor.Extract(description);
        var nearMetro = NearMetroExtractor.Extract(description);
        var hasUsageLicense = UsageLicenseExtractor.Extract(description);
        var energyRating = EnergyRatingExtractor.Extract(description);
        var price = item.Price.HasValue ? (decimal?)item.Price.Value : null;
        var size = item.Size.HasValue ? (decimal?)item.Size.Value : null;

        var photos = string.IsNullOrEmpty(item.Thumbnail) ? Array.Empty<string>() : new[] { item.Thumbnail };

        // commercialName (agency/branch name) is preferred over contactName
        // (often just a generic label like "Agente" on real results) when
        // both are present; falls back to whichever is populated.
        var agentName = item.ContactInfo?.CommercialName ?? item.ContactInfo?.ContactName;
        var agentPhone = item.ContactInfo?.Phone1?.PhoneNumber;

        return new Property
        {
            Url = item.Url ?? $"https://www.idealista.pt/imovel/{item.PropertyCode}/",
            Source = Source,
            Price = price,
            LocationString = locationString,
            Distrito = district,
            Concelho = concelho,
            Freguesia = freguesia,
            Lat = item.Latitude,
            Lng = item.Longitude,
            Beds = item.Rooms,
            Baths = item.Bathrooms,
            SizeM2 = size,
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
            AgentName = agentName,
            AgentPhone = agentPhone,
            PhotosJson = JsonSerializer.Serialize(photos),
            SourcePropertyId = item.PropertyCode,
            DedupHash = DedupHashGenerator.Compute(locationString, price ?? 0, item.Rooms, size)
        };
    }
}
