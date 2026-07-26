using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using EstateAggregator.Utilities;
using HtmlAgilityPack;

namespace EstateAggregator.Services.Scrapers;

// Real integration, verified against the live site: casa.sapo.pt is a
// traditional server-rendered page (no embedded JSON like ImoVirtual), but
// listing cards use stable, semantic CSS classes (property-type,
// property-location, property-price-value, property-description, ...), so
// this parses the rendered HTML directly with HtmlAgilityPack.
//
// The site's own price/rooms query parameters could not be confirmed
// reliably in the time available, so this scraper takes the simpler, more
// robust route: fetch by district only, parse every listing, and apply the
// user's price/rooms filters in code — that way correctness doesn't depend
// on guessed query-string behavior.
//
// The site doesn't expose a single wrapper element per listing card with a
// unique class; property-info-content/property-description/property-media
// each appear once per card in the same document order, so they're queried
// separately and zipped by index rather than walked as one subtree.
public class CasaSapoScraper : IPropertyScraper
{
    private readonly HttpClient _httpClient;
    private readonly EstateDbContext _db;
    private readonly DeduplicationService _dedupService;
    private readonly ILogger<CasaSapoScraper> _logger;
    private bool _hasMadeFirstRequest;

    public string Source => "CasaSapo";

    // casa.sapo.pt's rate limiting turned out to trigger on total requests
    // in a short window, not just back-to-back spacing (5 quick Lisboa
    // pages left no budget for Porto's very first request even a full
    // second later). Kept conservative: fewer pages, longer gaps.
    private const int MaxPagesPerDistrict = 3;
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(2500);

    private static readonly Regex RoomsRegex = new(@"[Tt](\d+)", RegexOptions.Compiled);
    private static readonly Regex SizeRegex = new(@"(\d+(?:[.,]\d+)?)\s*m", RegexOptions.Compiled);
    private static readonly Regex TrackingUrlRegex = new(@"[?&]l=([^&]+)", RegexOptions.Compiled);

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
    };

    public CasaSapoScraper(HttpClient httpClient, EstateDbContext db, DeduplicationService dedupService, ILogger<CasaSapoScraper> logger)
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

        for (var page = 1; page <= MaxPagesPerDistrict; page++)
        {
            // Delay before every request except the very first of the whole
            // run — not just between pages within one district. Without
            // this, finishing district A's last page and immediately
            // starting district B's first page back-to-back was enough to
            // trip the site's rate limiting (HTTP 429).
            if (_hasMadeFirstRequest)
                await Task.Delay(DelayBetweenRequests, cancellationToken);
            _hasMadeFirstRequest = true;

            var url = $"https://casa.sapo.pt/comprar-apartamentos/{slug}/" + (page > 1 ? $"?pn={page}" : string.Empty);

            string html;
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("CasaSapo returned {Status} for {District} page {Page}", response.StatusCode, district, page);
                    report.HasErrors = true;
                    report.Errors.Add($"{district}: HTTP {(int)response.StatusCode}");
                    return;
                }

                html = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CasaSapo request failed for {District} page {Page}", district, page);
                report.HasErrors = true;
                report.Errors.Add($"{district}: {ex.Message}");
                return;
            }

            List<Property> candidates;
            try
            {
                candidates = ParseListings(html);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CasaSapo parse failed for {District} page {Page}", district, page);
                report.HasErrors = true;
                report.Errors.Add($"{district}: parse failed ({ex.Message})");
                return;
            }

            if (candidates.Count == 0)
                return;

            report.PropertiesFound += candidates.Count;

            foreach (var candidate in candidates)
            {
                if (candidate.Price.HasValue && (candidate.Price < settings.PriceMin || candidate.Price > settings.PriceMax))
                    continue;

                if (candidate.Beds.HasValue && (candidate.Beds < settings.RoomsMin || candidate.Beds > settings.RoomsMax))
                    continue;

                var outcome = await _dedupService.ProcessAsync(_db, candidate);
                switch (outcome)
                {
                    case DedupOutcome.Added: report.PropertiesAdded++; break;
                    case DedupOutcome.Updated: report.PropertiesUpdated++; break;
                    case DedupOutcome.Skipped: report.PropertiesSkipped++; break;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private List<Property> ParseListings(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var infoNodes = doc.DocumentNode.SelectNodes("//div[@class='property-info-content']");
        if (infoNodes == null || infoNodes.Count == 0)
            return new List<Property>();

        var descNodes = doc.DocumentNode.SelectNodes("//div[@class='property-description']");
        var mediaNodes = doc.DocumentNode.SelectNodes("//div[@class='property-media']");

        var results = new List<Property>();

        for (var i = 0; i < infoNodes.Count; i++)
        {
            var property = ParseOne(
                infoNodes[i],
                i < (descNodes?.Count ?? 0) ? descNodes![i] : null,
                i < (mediaNodes?.Count ?? 0) ? mediaNodes![i] : null);

            if (property != null)
                results.Add(property);
        }

        return results;
    }

    private Property? ParseOne(HtmlNode infoNode, HtmlNode? descNode, HtmlNode? mediaNode)
    {
        var anchor = infoNode.SelectSingleNode(".//a[@class='property-info']");
        var href = anchor?.GetAttributeValue("href", string.Empty);
        var url = ExtractRealUrl(href);
        if (string.IsNullOrEmpty(url))
            return null;

        var typeText = infoNode.SelectSingleNode(".//div[@class='property-type']")?.InnerText.Trim() ?? string.Empty;
        var locationText = HtmlEntity.DeEntitize(infoNode.SelectSingleNode(".//div[@class='property-location']")?.InnerText.Trim() ?? "Unknown");
        var featuresText = infoNode.SelectSingleNode(".//div[@class='property-features-text']")?.InnerText.Trim() ?? string.Empty;
        var priceText = infoNode.SelectSingleNode(".//div[@class='property-price-value']")?.InnerText.Trim() ?? string.Empty;
        var description = HtmlEntity.DeEntitize(descNode?.InnerText.Trim() ?? string.Empty);

        var beds = ParseRooms(typeText);
        var size = ParseSize(featuresText);
        var price = ParsePrice(priceText);
        var photoUrl = ExtractPhotoUrl(mediaNode);

        var (orientation, orientationSource) = OrientationExtractor.Extract(description);

        return new Property
        {
            Url = url,
            Source = Source,
            Price = price,
            LocationString = locationText,
            Beds = beds,
            Baths = null,
            SizeM2 = size,
            Description = description,
            SunOrientation = orientation,
            OrientationSource = orientationSource,
            PhotosJson = JsonSerializer.Serialize(photoUrl != null ? new[] { photoUrl } : Array.Empty<string>()),
            SourcePropertyId = mediaNode?.GetAttributeValue("data-uid", string.Empty),
            DedupHash = DedupHashGenerator.Compute(locationText, price ?? 0, beds)
        };
    }

    private static string? ExtractRealUrl(string? trackingHref)
    {
        if (string.IsNullOrEmpty(trackingHref))
            return null;

        var match = TrackingUrlRegex.Match(trackingHref);
        if (!match.Success)
            return trackingHref.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? trackingHref : null;

        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private static string? ExtractPhotoUrl(HtmlNode? mediaNode)
    {
        var source = mediaNode?.SelectSingleNode(".//source");
        var srcset = source?.GetAttributeValue("srcset", string.Empty);
        if (string.IsNullOrEmpty(srcset))
            return null;

        // srcset is a comma-separated "url 1x, url 2x, url 4x" list; take the first URL.
        var firstEntry = srcset.Split(',')[0].Trim();
        var url = firstEntry.Split(' ')[0];
        return string.IsNullOrEmpty(url) ? null : url;
    }

    private static int? ParseRooms(string typeText)
    {
        var match = RoomsRegex.Match(typeText);
        return match.Success && int.TryParse(match.Groups[1].Value, out var rooms) ? rooms : null;
    }

    private static decimal? ParseSize(string featuresText)
    {
        var match = SizeRegex.Match(featuresText);
        if (!match.Success)
            return null;

        var raw = match.Groups[1].Value.Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var size) ? size : null;
    }

    private static decimal? ParsePrice(string priceText)
    {
        // Portuguese formatting uses "." as a thousands separator, e.g. "490.000 €".
        var digitsOnly = new string(priceText.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digitsOnly, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            ? price
            : null;
    }

    private static string ResolveDistrictSlug(string district)
    {
        var trimmed = district.Trim();
        return DistrictSlugAliases.TryGetValue(trimmed, out var slug)
            ? slug
            : DedupHashGenerator.NormalizeLocation(trimmed).Replace(' ', '-');
    }
}
