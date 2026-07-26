using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EstateAggregator.Data;
using EstateAggregator.DTOs;
using EstateAggregator.Models;
using EstateAggregator.Utilities;
using HtmlAgilityPack;

namespace EstateAggregator.Services.Scrapers;

// Real integration, verified against the live site: imoveis.santander.pt is
// Banco Santander Totta's real estate arm, selling repossessed/bank-owned
// properties. Clean semantic markup (.prop, .prop-title, .prop-description),
// parsed here with HtmlAgilityPack. Descriptions embed room/bathroom counts
// in Portuguese ("N sala(s), N quarto(s) e N wc(s)"), extracted via regex.
//
// KNOWN LIMITATION: the site's listing page is built on a Casafari CRM
// widget whose pagination is JS/AJAX-driven — page=/pagina=/p= query params
// were all tried and none actually changed the results, so only the
// listings on the single default page (~12) are reachable this way. Price
// and district filtering are likewise done in code rather than via query
// params, since no working query-string filter could be found either.
public class SantanderScraper : IPropertyScraper
{
    private readonly HttpClient _httpClient;
    private readonly EstateDbContext _db;
    private readonly DeduplicationService _dedupService;
    private readonly ILogger<SantanderScraper> _logger;

    public string Source => "Santander";

    private static readonly Regex RoomsRegex = new(@"(\d+)\s*quarto", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BathsRegex = new(@"(\d+)\s*wc", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AreaRegex = new(@"Área bruta:\s*([\d.,]+)\s*m", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public SantanderScraper(HttpClient httpClient, EstateDbContext db, DeduplicationService dedupService, ILogger<SantanderScraper> logger)
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

        string html;
        try
        {
            var response = await _httpClient.GetAsync("https://imoveis.santander.pt/imoveis", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Santander returned {Status}", response.StatusCode);
                report.HasErrors = true;
                report.Errors.Add($"HTTP {(int)response.StatusCode}");
                report.EndTime = DateTime.UtcNow;
                return report;
            }

            html = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Santander request failed");
            report.HasErrors = true;
            report.Errors.Add(ex.Message);
            report.EndTime = DateTime.UtcNow;
            return report;
        }

        List<Property> candidates;
        try
        {
            candidates = ParseListings(html);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Santander parse failed");
            report.HasErrors = true;
            report.Errors.Add($"Parse failed: {ex.Message}");
            report.EndTime = DateTime.UtcNow;
            return report;
        }

        report.PropertiesFound += candidates.Count;

        foreach (var candidate in candidates)
        {
            if (normalizedDistricts.Count > 0)
            {
                var normalizedLocation = DedupHashGenerator.NormalizeLocation(candidate.LocationString);
                if (!normalizedDistricts.Any(d => normalizedLocation.Contains(d)))
                    continue;
            }

            if (candidate.Price.HasValue && (candidate.Price < settings.PriceMin || candidate.Price > settings.PriceMax))
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

        report.EndTime = DateTime.UtcNow;
        return report;
    }

    private List<Property> ParseListings(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var propNodes = doc.DocumentNode.SelectNodes("//div[@class='prop']");
        if (propNodes == null)
            return new List<Property>();

        var results = new List<Property>();
        foreach (var node in propNodes)
        {
            var property = ParseOne(node);
            if (property != null)
                results.Add(property);
        }

        return results;
    }

    private Property? ParseOne(HtmlNode node)
    {
        var anchor = node.SelectSingleNode(".//div[@class='prop-imgWrap']//a[@href]");
        var href = anchor?.GetAttributeValue("href", string.Empty);
        if (string.IsNullOrEmpty(href))
            return null;

        var url = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? href
            : "https://imoveis.santander.pt" + href;

        var typeTag = HtmlEntity.DeEntitize(node.SelectSingleNode(".//div[@class='prop-tag']")?.InnerText.Trim() ?? string.Empty);

        // HtmlAgilityPack's InnerText does NOT decode HTML entities — the
        // raw text here is literally "85&#160;000 €", and without
        // DeEntitize() the "160" inside "&#160;" gets swept up by the
        // digit-only price parser below, producing wildly wrong prices
        // (e.g. 85000 became 85160000). Caught by comparing scraped prices
        // against the real site.
        var priceText = HtmlEntity.DeEntitize(
            node.SelectSingleNode(".//div[contains(@class,'prop-tag-sub')]")?.InnerText ?? string.Empty);
        var price = ParsePrice(priceText);

        var titleFirst = HtmlEntity.DeEntitize(node.SelectSingleNode(".//span[@class='prop-titleFirst']")?.InnerText.Trim() ?? string.Empty);
        var titleSecond = HtmlEntity.DeEntitize(node.SelectSingleNode(".//span[@class='prop-titleSecond']")?.InnerText.Trim() ?? string.Empty);
        var location = !string.IsNullOrEmpty(titleFirst) && !string.IsNullOrEmpty(titleSecond)
            ? $"{titleFirst}, {titleSecond}"
            : titleFirst.Length > 0 ? titleFirst : "Unknown";

        var descriptionNode = node.SelectSingleNode(".//div[@class='prop-description']");
        var description = HtmlEntity.DeEntitize(descriptionNode?.InnerText.Trim() ?? string.Empty);

        var specsText = HtmlEntity.DeEntitize(node.SelectSingleNode(".//div[contains(@class,'prop-specsWrapInner')]")?.InnerText ?? string.Empty);

        var beds = ParseIntGroup(RoomsRegex, specsText + description);
        var baths = ParseIntGroup(BathsRegex, specsText + description);
        var size = ParseSize(specsText);

        var photoStyle = node.SelectSingleNode(".//div[@class='prop-imgArea']")?.GetAttributeValue("style", string.Empty) ?? string.Empty;
        var photoMatch = Regex.Match(photoStyle, @"url\('([^']+)'\)");
        var photoUrl = photoMatch.Success ? photoMatch.Groups[1].Value : null;

        var fullDescription = $"{typeTag} — {description}".Trim(' ', '—');
        var (orientation, orientationSource) = OrientationExtractor.Extract(fullDescription);

        return new Property
        {
            Url = url,
            Source = Source,
            Price = price,
            LocationString = location,
            Beds = beds,
            Baths = baths,
            SizeM2 = size,
            Description = fullDescription,
            SunOrientation = orientation,
            OrientationSource = orientationSource,
            PhotosJson = JsonSerializer.Serialize(photoUrl != null ? new[] { photoUrl } : Array.Empty<string>()),
            SourcePropertyId = ExtractRef(typeTag),
            DedupHash = DedupHashGenerator.Compute(location, price ?? 0, beds)
        };
    }

    private static string? ExtractRef(string typeTag)
    {
        var match = Regex.Match(typeTag, @"Ref[ª:]*\s*:?\s*(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int? ParseIntGroup(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }

    private static decimal? ParseSize(string specsText)
    {
        var match = AreaRegex.Match(specsText);
        if (!match.Success)
            return null;

        var raw = match.Groups[1].Value.Replace(".", "").Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var size) && size > 0
            ? size
            : null;
    }

    private static decimal? ParsePrice(string priceText)
    {
        // Site uses non-breaking spaces as thousands separators, e.g. "195 000 €".
        var digitsOnly = new string(priceText.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digitsOnly, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            ? price
            : null;
    }
}
