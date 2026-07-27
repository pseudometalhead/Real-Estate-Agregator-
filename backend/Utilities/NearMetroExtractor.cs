using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Only ever true or null, like OpenPlanKitchenExtractor — a listing not
// mentioning the metro doesn't mean it's far from one, just that it wasn't
// called out. Anchored to "metro" specifically (not "transportes públicos"
// generically) since metro proximity is the strongest, most search-relevant
// signal and the one most consistently phrased the same way across
// listings ("perto do metro", "estação de metro a X metros").
public static class NearMetroExtractor
{
    private static readonly Regex Pattern = new(
        @"(esta[çc][ãa]o (do |de )?)?metro\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool? Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        return Pattern.IsMatch(description) ? true : null;
    }
}
