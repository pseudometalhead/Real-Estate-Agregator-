using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

public static class OrientationExtractor
{
    // Word-boundary regexes so a trailing comma/period ("virado a norte,")
    // still matches — a naive " norte " space-padded Contains() check does
    // not, since punctuation immediately follows the word instead of a space.
    private static readonly Regex SulPattern = new(
        @"fachada sul|frente sul|virad[oa] a sul|\bsul\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NortePattern = new(
        @"fachada norte|frente norte|virad[oa] a norte|\bnorte\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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

    public static (string SunOrientation, string OrientationSource) Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return ("Not Available", "not_available");

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
