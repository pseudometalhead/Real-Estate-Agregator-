using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Tri-state like ElevatorExtractor. Covers both "varanda" (balcony) and
// "terraço" (terrace) under one flag — the distinction rarely matters for a
// house hunt filter ("some private outdoor space or not"), and the app has
// no other feature that would need them told apart.
public static class BalconyExtractor
{
    private static readonly Regex NoBalconyPattern = new(
        @"sem varanda|sem terra[çc]o|n[ãa]o (tem|possui|disp[õo]e de) varanda|n[ãa]o (tem|possui|disp[õo]e de) terra[çc]o",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HasBalconyPattern = new(
        @"\bvaranda\b|\bterra[çc]o\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool? Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        if (NoBalconyPattern.IsMatch(description))
            return false;

        if (HasBalconyPattern.IsMatch(description))
            return true;

        return null;
    }
}
