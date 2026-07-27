using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Only ever returns true or null, like OpenPlanKitchenExtractor — "recently
// renovated" is orthogonal to ConstructionStatusExtractor's build-stage
// category (an old building can still have been recently renovated), so
// this is a separate flag, not a replacement. There's no reliable false
// case: ConstructionStatus already owns "needs renovation" (Para
// Recuperar) — a listing simply not mentioning renovation doesn't mean it
// wasn't renovated, just that it wasn't advertised as a selling point.
public static class RenovatedExtractor
{
    private static readonly Regex Pattern = new(
        @"(totalmente |recentemente |completamente )?remodelad[ao]|(totalmente |recentemente |completamente )?renovad[ao]|obras de renova[çc][ãa]o (recentes|conclu[íi]das)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool? Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        return Pattern.IsMatch(description) ? true : null;
    }
}
