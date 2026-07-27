using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Only ever true or null, like OpenPlanKitchenExtractor — there's no
// reliable way to confirm a river/sea view is absent from silence, only
// that it was or wasn't mentioned. Covers both "vista mar/rio" and naming
// the Douro directly (Porto's river, mentioned by name in listings at
// least as often as the generic "vista rio" phrase).
public static class WaterViewExtractor
{
    private static readonly Regex Pattern = new(
        @"vista\s+(para\s+o\s+|do\s+)?(mar|rio|douro)\b|vista\s+(mar|rio|douro)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool? Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        return Pattern.IsMatch(description) ? true : null;
    }
}
