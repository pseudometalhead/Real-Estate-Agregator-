using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

public static class OrientationExtractor
{
    // Word-boundary regexes so a trailing comma/period ("virado a norte,")
    // still matches — a naive " norte " space-padded Contains() check does
    // not, since punctuation immediately follows the word instead of a space.
    //
    // Bare \bsul\b / \bnorte\b are deliberately NOT matched: verified against
    // 334 real ImoVirtual listings, both showed up in place/brand names
    // unrelated to sun orientation — "Norte Shopping" (a mall) and
    // "Matosinhos Sul" (a neighborhood) both got misclassified. "sul"/"norte"
    // are only trusted in phrases that unambiguously mean sun exposure.
    private static readonly Regex SulPattern = new(
        @"fachada sul|frente sul|virad[oa] a sul|volt(ado|ada) a sul|\ba sul\b|exposi[cç][ãa]o (solar )?a sul",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NortePattern = new(
        @"fachada norte|frente norte|virad[oa] a norte|volt(ado|ada) a norte|\ba norte\b|exposi[cç][ãa]o (solar )?a norte",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // "este" is deliberately NOT matched as a bare word: in Portuguese it's
    // the extremely common demonstrative "this" ("este apartamento", "este
    // T4", ...), not just the cardinal direction. Tested against 37 real
    // ImoVirtual listings: a bare \beste\b match produced 15 false positives
    // out of 16 total matches (only "nascente" was a genuine sun-orientation
    // mention). "este" is only trusted here in compound phrases that
    // unambiguously refer to sun exposure.
    private static readonly Regex OrientePattern = new(
        @"nascente|oriente|virad[oa] a (nascente|este|leste)|exposi[cç][ãa]o (solar )?a este|fachada este|\bleste\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PoentePattern = new(
        @"poente|virad[oa] a (poente|oeste)|\boeste\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Compass-abbreviation form ("Orientação solar: N/E", "Exposição: SO"),
    // seen verbatim on CustoJusto listings alongside the prose forms above.
    // Bare "N"/"S"/"E"/"O" letters are far too ambiguous to trust anywhere in
    // free text (single letters collide with everything), so this only
    // matches immediately after an explicit "orientação"/"exposição (solar)"
    // label — the same standard this file already applies to "norte"/"sul"
    // (see the comment above SulPattern/NortePattern). The captured letters
    // are normalized and mapped below; two-letter order (N/E vs E/N) doesn't
    // matter since both mean the same intercardinal direction.
    private static readonly Regex CompassAbbreviationPattern = new(
        @"(?:orienta[cç][ãa]o|exposi[cç][ãa]o)(?:\s+solar)?\s*:?\s*([nselo][/\-]?[nselo]?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> CompassAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["N"] = "Norte",
        ["S"] = "Sul",
        ["E"] = "Oriente",
        ["L"] = "Oriente", // "L" for Leste, an alternate abbreviation for East seen alongside "E"
        ["O"] = "Poente",
        ["NE"] = "Norte/Nascente",
        ["EN"] = "Norte/Nascente",
        ["NL"] = "Norte/Nascente",
        ["LN"] = "Norte/Nascente",
        ["NO"] = "Norte/Poente",
        ["ON"] = "Norte/Poente",
        ["SE"] = "Sul/Nascente",
        ["ES"] = "Sul/Nascente",
        ["SL"] = "Sul/Nascente",
        ["LS"] = "Sul/Nascente",
        ["SO"] = "Sul/Poente",
        ["OS"] = "Sul/Poente",
    };

    public static (string SunOrientation, string OrientationSource) Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return ("Not Available", "not_available");

        var compassMatch = CompassAbbreviationPattern.Match(description);
        if (compassMatch.Success)
        {
            var letters = compassMatch.Groups[1].Value.Replace("/", "").Replace("-", "").ToUpperInvariant();
            if (CompassAbbreviations.TryGetValue(letters, out var compassOrientation))
                return (compassOrientation, "extracted");
        }

        if (SulPattern.IsMatch(description))
            return ("Sul", "extracted");

        if (NortePattern.IsMatch(description))
            return ("Norte", "extracted");

        if (OrientePattern.IsMatch(description))
            return ("Oriente", "extracted");

        if (PoentePattern.IsMatch(description))
            return ("Poente", "extracted");

        return ("Not Available", "not_available");
    }
}
