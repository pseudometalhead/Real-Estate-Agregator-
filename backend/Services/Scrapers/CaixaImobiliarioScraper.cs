using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using EstateAggregator.Utilities;
using HtmlAgilityPack;

namespace EstateAggregator.Services.Scrapers;

// Real integration, verified against the live site: caixaimobiliario.pt is
// Caixa Geral de Depósitos' (a Portuguese bank) real estate arm, selling
// repossessed/bank-owned properties. Traditional server-rendered JSP pages
// (ISO-8859-1 encoded) with stable semantic classes (mod_imovel,
// result_imovel_txt), parsed here with HtmlAgilityPack.
//
// The site's price filter (pcmin/pcmax query params) works and is used
// directly. Its district filter ("dc" select) is populated dynamically by
// JS and its option values couldn't be determined from static HTML, so
// district filtering is instead done in code by matching the "District |
// Municipality" text each listing already shows against the configured
// districts — same pragmatic approach as CasaSapoScraper.
public class CaixaImobiliarioScraper : IPropertyScraper
{
    private readonly HttpClient _httpClient;
    private readonly EstateDbContext _db;
    private readonly DeduplicationService _dedupService;
    private readonly ILogger<CaixaImobiliarioScraper> _logger;

    public string Source => "CaixaImobiliario";

    private const int PageSize = 8;
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(1200);

    private static readonly Regex RoomsRegex = new(@"[Tt](\d+)\b", RegexOptions.Compiled);

    // A listing's own href carries the site's search-context query params
    // (pos/pgnr/ofs/rsnr — position within whatever results page it happened
    // to be found on) alongside the actual listing id. Verified live: the
    // SAME listing (same id=) recurs across every page of a search whose
    // real result count is smaller than MaxPagesPerSource, each time with a
    // different pos/pgnr — keeping those params in Url made 49 re-encounters
    // of one physical listing look like 49 distinct URLs, which
    // DeduplicationService then linked to each other as fake "also listed
    // on Caixa Imobiliário [itself]" duplicates. Only "id" actually
    // identifies the listing, so that's all the canonical Url keeps.
    private static readonly Regex IdParamRegex = new(@"[?&]id=(\d+)", RegexOptions.Compiled);

    private bool _hasMadeFirstRequest;

    public CaixaImobiliarioScraper(HttpClient httpClient, EstateDbContext db, DeduplicationService dedupService, ILogger<CaixaImobiliarioScraper> logger)
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

        // Resolve English aliases ("Lisbon") to their Portuguese form
        // ("Lisboa") before normalizing — listing text is in Portuguese, so
        // "Lisbon" would never match "Lisboa" as a plain substring.
        var normalizedDistricts = districts
            .Select(DistrictAliases.ToPortuguese)
            .Select(DedupHashGenerator.NormalizeLocation)
            .ToList();
        _hasMadeFirstRequest = false;

        for (var page = 1; page <= settings.MaxPagesPerSource; page++)
        {
            if (_hasMadeFirstRequest)
                await Task.Delay(DelayBetweenRequests, cancellationToken);
            _hasMadeFirstRequest = true;

            var priceMin = settings.PriceMin > 0 ? ((int)settings.PriceMin).ToString(CultureInfo.InvariantCulture) : "-1";
            var priceMax = settings.PriceMax > 0 ? ((int)settings.PriceMax).ToString(CultureInfo.InvariantCulture) : "0";
            var url = "https://www.caixaimobiliario.pt/comprar/imoveis-venda.jsp" +
                      $"?pcmin={priceMin}&pcmax={priceMax}&pgnr={page}&pgsz={PageSize}&listing=resumo&ordby=data_entrada";

            string html;
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("CaixaImobiliario returned {Status} for page {Page}", response.StatusCode, page);
                    report.HasErrors = true;
                    report.Errors.Add($"HTTP {(int)response.StatusCode} on page {page}");
                    break;
                }

                // Site serves ISO-8859-1; HttpContent honors the charset in
                // the Content-Type header automatically.
                html = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CaixaImobiliario request failed for page {Page}", page);
                report.HasErrors = true;
                report.Errors.Add($"Page {page}: {ex.Message}");
                break;
            }

            List<Property> candidates;
            try
            {
                candidates = ParseListings(html, normalizedDistricts);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CaixaImobiliario parse failed for page {Page}", page);
                report.HasErrors = true;
                report.Errors.Add($"Page {page}: parse failed ({ex.Message})");
                break;
            }

            if (candidates.Count == 0)
                break;

            report.PropertiesFound += candidates.Count;

            foreach (var candidate in candidates)
            {
                var outcome = await _dedupService.ProcessAsync(_db, candidate);
                switch (outcome)
                {
                    case DedupOutcome.Added: report.PropertiesAdded++; break;
                    case DedupOutcome.Updated: report.PropertiesUpdated++; break;
                    case DedupOutcome.Linked: report.PropertiesLinked++; break;
                    case DedupOutcome.Skipped: report.PropertiesSkipped++; break;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        report.EndTime = DateTime.UtcNow;
        return report;
    }

    private List<Property> ParseListings(string html, List<string> normalizedDistricts)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var infoNodes = doc.DocumentNode.SelectNodes("//div[@class='result_imovel_txt']");
        if (infoNodes == null || infoNodes.Count == 0)
            return new List<Property>();

        var results = new List<Property>();

        foreach (var node in infoNodes)
        {
            var property = ParseOne(node, normalizedDistricts);
            if (property != null)
                results.Add(property);
        }

        return results;
    }

    private Property? ParseOne(HtmlNode node, List<string> normalizedDistricts)
    {
        var anchor = node.SelectSingleNode(".//a[@href]");
        var href = anchor?.GetAttributeValue("href", string.Empty);
        if (string.IsNullOrEmpty(href))
            return null;

        var url = CanonicalizeUrl(
            href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : "https://www.caixaimobiliario.pt" + href);

        var typeText = HtmlEntity.DeEntitize(anchor?.InnerText.Trim() ?? string.Empty);

        var fullText = HtmlEntity.DeEntitize(node.InnerText) ?? string.Empty;
        var priceMatch = Regex.Match(fullText, @"([\d.]+)\s*€");
        var price = priceMatch.Success ? ParsePrice(priceMatch.Groups[1].Value) : null;

        // Location renders as "District | Municipality" on its own line.
        var locationMatch = Regex.Match(fullText, @"([A-ZÀ-Ýa-zà-ÿ][^\n|]{1,40})\s*\|\s*([^\n]{1,40})");
        var locationDistrict = locationMatch.Success ? locationMatch.Groups[1].Value.Trim() : null;
        var locationMunicipality = locationMatch.Success ? locationMatch.Groups[2].Value.Trim() : null;
        var location = locationMatch.Success
            ? $"{locationDistrict}, {locationMunicipality}"
            : "Unknown";

        if (normalizedDistricts.Count > 0)
        {
            var normalizedLocation = DedupHashGenerator.NormalizeLocation(location);
            var matchesConfiguredDistrict = normalizedDistricts.Any(d => normalizedLocation.Contains(d));
            if (!matchesConfiguredDistrict)
                return null;
        }

        var refMatch = Regex.Match(fullText, @"Ref\.\s*([\w/]+)");
        var sourcePropertyId = refMatch.Success ? refMatch.Groups[1].Value : null;

        // node.InnerText concatenates EVERYTHING in the card — price,
        // "District | Municipality", the size line, ref — with no
        // separators, so using it as-is for Description dumped raw
        // whitespace and redundant labels straight to the user (verified
        // live: https://www.caixaimobiliario.pt/comprar/imoveis-venda.jsp).
        // The actual free-text blurb lives in its own plain <span> (no
        // attributes), sitting between the "54 m² de área bruta" span
        // (which DOES have a style attribute) and the "Ref. ..." span
        // (also bare, but always starts with "Ref.").
        var descriptionSpanText = node.SelectNodes(".//span[not(@style)]")?
            .Select(s => HtmlEntity.DeEntitize(s.InnerText)?.Trim() ?? string.Empty)
            .FirstOrDefault(t => t.Length > 0 && !t.StartsWith("Ref.", StringComparison.OrdinalIgnoreCase));
        var cleanDescription = string.IsNullOrEmpty(descriptionSpanText)
            ? typeText
            : $"{typeText} — {Regex.Replace(descriptionSpanText, @"\s+", " ").Trim()}";

        // Same span-based approach for size: "54 m² de área bruta" lives in
        // the one <span style="..."> in the card (every other span here is
        // bare) — regexing the whole blob risked matching digits from the
        // price or ref instead.
        var sizeSpanText = node.SelectSingleNode(".//span[@style]")?.InnerText;
        var sizeMatch = string.IsNullOrEmpty(sizeSpanText) ? null : Regex.Match(sizeSpanText, @"(\d+(?:[.,]\d+)?)\s*m");
        var size = sizeMatch is { Success: true }
            ? decimal.Parse(sizeMatch.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture)
            : (decimal?)null;

        // Extractors still scan the full raw text blob (fullText), not just
        // the isolated description span — more recall, and none of these
        // patterns can false-positive on a price or "District | Municipality"
        // line, so there's nothing to lose by including them.
        var extractionText = typeText + (string.IsNullOrEmpty(typeText) ? "" : " — ") + fullText;
        var (orientation, orientationSource) = OrientationExtractor.Extract(extractionText);
        var openPlanKitchen = OpenPlanKitchenExtractor.Extract(extractionText);
        var constructionStatus = ConstructionStatusExtractor.Extract(extractionText);
        var elevator = ElevatorExtractor.Extract(extractionText);
        var parking = ParkingExtractor.Extract(extractionText);
        var furnished = FurnishedExtractor.Extract(extractionText);
        var airConditioning = AirConditioningExtractor.Extract(extractionText);
        var balcony = BalconyExtractor.Extract(extractionText);
        var renovated = RenovatedExtractor.Extract(extractionText);
        var storage = StorageExtractor.Extract(extractionText);
        var waterView = WaterViewExtractor.Extract(extractionText);
        var nearMetro = NearMetroExtractor.Extract(extractionText);
        var hasUsageLicense = UsageLicenseExtractor.Extract(extractionText);
        var energyRating = EnergyRatingExtractor.Extract(extractionText);
        var beds = ParseRooms(typeText);

        var photoUrl = node.SelectSingleNode("preceding-sibling::div[@class='mod_imovel'][1]//img")?.GetAttributeValue("src", string.Empty)
            ?? node.ParentNode?.SelectSingleNode(".//img")?.GetAttributeValue("src", string.Empty);
        photoUrl = string.IsNullOrEmpty(photoUrl) ? null : photoUrl;

        return new Property
        {
            Url = url,
            Source = Source,
            Price = price,
            LocationString = location,
            Distrito = locationDistrict,
            Concelho = locationMunicipality,
            Beds = beds,
            Baths = null,
            SizeM2 = size,
            Description = DescriptionCleaner.Clean(cleanDescription.Trim()),
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
            PhotosJson = JsonSerializer.Serialize(photoUrl != null ? new[] { photoUrl } : Array.Empty<string>()),
            SourcePropertyId = sourcePropertyId,
            DedupHash = DedupHashGenerator.Compute(location, price ?? 0, beds, size)
        };
    }

    private static string CanonicalizeUrl(string url)
    {
        var idMatch = IdParamRegex.Match(url);
        if (!idMatch.Success)
            return url; // Defensive fallback — unexpected shape, keep the raw href rather than losing the listing.

        var path = url[..url.IndexOf('?')];
        return $"{path}?id={idMatch.Groups[1].Value}";
    }

    private static int? ParseRooms(string typeText)
    {
        var match = RoomsRegex.Match(typeText);
        return match.Success && int.TryParse(match.Groups[1].Value, out var rooms) ? rooms : null;
    }

    private static decimal? ParsePrice(string priceText)
    {
        // Portuguese formatting: "." as thousands separator, e.g. "9.900.000".
        var digitsOnly = new string(priceText.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digitsOnly, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            ? price
            : null;
    }
}
