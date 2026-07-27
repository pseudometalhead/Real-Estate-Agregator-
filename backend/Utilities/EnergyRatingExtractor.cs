using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Unlike every other extractor here, this pulls out a categorical value
// (A+ through F), not a tri-state fact — Portuguese energy performance
// certificates use a fixed A+/A/B/B-/C/D/E/F scale, mandatory on virtually
// every listing ("Certificado Energético: B-", "Categoria Energética: D",
// "Classe Energética A"), so it's high-hit-rate and worth its own field
// rather than folding into ConstructionStatus (which is about build stage,
// not efficiency).
public static class EnergyRatingExtractor
{
    // No trailing \b: a grade ending in "-" (e.g. "B-") sitting at the end of
    // the description, or immediately before a period, isn't a \w/\W
    // boundary on the "-" side, so a trailing \b would fail to match exactly
    // the common "Certificado Energético: B-" (end of string) case. The
    // negative lookahead instead guards against capturing the start of an
    // unrelated word right after "energético" (e.g. "energético a solicitar"
    // should not read as grade "A").
    private static readonly Regex Pattern = new(
        @"(?:certificado|classe|categoria)\s+energ[ée]tic[ao]\s*:?\s*([A-G][+-]?)(?![a-zçãõáéíóúâêô])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var match = Pattern.Match(description);
        if (!match.Success)
            return null;

        return match.Groups[1].Value.ToUpperInvariant();
    }
}
