using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Same tri-state reasoning as ElevatorExtractor: "sem garagem" is a genuine,
// common, explicit negative in PT listings, so this can confidently return
// false rather than only ever true/null.
public static class ParkingExtractor
{
    private static readonly Regex NoParkingPattern = new(
        @"sem garagem|sem estacionamento|sem lugar de (garagem|estacionamento)|n[ãa]o (tem|possui|inclui|disp[õo]e de) (garagem|estacionamento)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "box" deliberately excluded on its own — too short/generic a token to
    // trust without a "garagem" anchor nearby, unlike "garagem" or
    // "estacionamento" which are unambiguous in this context.
    private static readonly Regex HasParkingPattern = new(
        @"garagem|estacionamento|parqueamento|lugar de garagem|box de garagem",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool? Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        if (NoParkingPattern.IsMatch(description))
            return false;

        if (HasParkingPattern.IsMatch(description))
            return true;

        return null;
    }
}
