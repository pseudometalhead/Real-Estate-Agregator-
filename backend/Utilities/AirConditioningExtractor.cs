using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Tri-state like ElevatorExtractor. Anchored specifically to "ar
// condicionado"/"ar-condicionado" — deliberately does NOT match the broader
// "climatização", which in PT listings can also mean central heating with no
// AC component, so treating it as equivalent would overclaim.
public static class AirConditioningExtractor
{
    private static readonly Regex NoAcPattern = new(
        @"sem ar[\s-]condicionado|n[ãa]o (tem|possui|disp[õo]e de) ar[\s-]condicionado",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HasAcPattern = new(
        @"ar[\s-]condicionado", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool? Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        if (NoAcPattern.IsMatch(description))
            return false;

        if (HasAcPattern.IsMatch(description))
            return true;

        return null;
    }
}
