using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Portuguese listings for reabilitação/restauro properties very often call
// out — explicitly and prominently — that the property has no "licença de
// utilização" (occupancy/usage license), usually because it predates 1951
// licensing requirements or is being sold under the Simplex Urbanístico
// regime (DL n.º 10/2024). That absence has real financial consequences for
// a buyer (no mortgage financing, cash-only purchase), so it's worth its own
// tri-state flag rather than being buried in free-text description.
//
// true (license confirmed present) is genuinely rare in listing copy — most
// properties just don't mention it because having one is the default,
// unremarkable case — so in practice this field is almost always either
// false or null, mirroring ElevatorExtractor's tri-state shape.
public static class UsageLicenseExtractor
{
    private static readonly Regex NoLicensePattern = new(
        @"sem licen[çc]a de (utiliza[çc][ãa]o|habita[çc][ãa]o|habitabilidade)" +
        @"|n[ãa]o (tem|possui|disp[õo]e de) licen[çc]a de (utiliza[çc][ãa]o|habita[çc][ãa]o|habitabilidade)" +
        @"|(isent[ao]|dispensad[ao]) de licen[çc]a de (utiliza[çc][ãa]o|habita[çc][ãa]o)" +
        @"|sem t[íi]tulos? urban[íi]stic[oa]" +
        @"|ao abrigo do simplex|regime simplex|simplex urban[íi]stico" +
        @"|decreto-lei n\.?[ºo°]?\s*10/2024",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HasLicensePattern = new(
        @"(possui|tem|com|dispõe de) licen[çc]a de (utiliza[çc][ãa]o|habita[çc][ãa]o)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool? Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        if (NoLicensePattern.IsMatch(description))
            return false;

        if (HasLicensePattern.IsMatch(description))
            return true;

        return null;
    }
}
