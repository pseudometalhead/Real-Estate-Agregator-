using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Tri-state like ElevatorExtractor/ParkingExtractor: listings often state the
// negative explicitly too ("não mobilado"), so absence of a positive match
// isn't enough to infer false — an explicit negative is required for that.
//
// Deliberately anchored to the "mobilad-/mobiliad-" word root only, NOT
// "equipado" alone — "cozinha equipada" (equipped kitchen: appliances built
// in) is present on nearly every listing regardless of whether the rest of
// the apartment has furniture, so using it as a trigger would make this
// return true almost universally and be meaningless as a filter.
public static class FurnishedExtractor
{
    private static readonly Regex NotFurnishedPattern = new(
        @"n[ãa]o\s*(est[áa]\s*)?mobiliad[ao]|n[ãa]o\s*(est[áa]\s*)?mobilad[ao]|sem mob[íi]lia",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FurnishedPattern = new(
        @"mobiliad[ao]|mobilad[ao]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool? Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        if (NotFurnishedPattern.IsMatch(description))
            return false;

        if (FurnishedPattern.IsMatch(description))
            return true;

        return null;
    }
}
