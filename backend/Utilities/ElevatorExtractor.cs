using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Unlike OrientationExtractor/OpenPlanKitchenExtractor, listings sometimes
// explicitly state the NEGATIVE too ("sem elevador" — a genuinely important
// fact for a 4th-floor walk-up), so this is a real tri-state: true
// (mentioned present), false (explicitly mentioned absent), or null (not
// mentioned either way).
public static class ElevatorExtractor
{
    // Checked first: an explicit negative must win over a bare "elevador"
    // match, since "sem elevador" itself contains the word "elevador".
    private static readonly Regex NoElevatorPattern = new(
        @"sem elevador|n[ãa]o (tem|possui|disp[õo]e de) elevador|desprovido de elevador|edif[íi]cio sem elevador",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HasElevatorPattern = new(
        @"\belevador\b|\bascensor\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool? Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        if (NoElevatorPattern.IsMatch(description))
            return false;

        if (HasElevatorPattern.IsMatch(description))
            return true;

        return null;
    }
}
