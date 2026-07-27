using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Tri-state like ElevatorExtractor — "arrecadação"/"arrumos" (storage room,
// separate from regular closets) is sometimes explicitly called out as
// absent, not just unmentioned.
public static class StorageExtractor
{
    private static readonly Regex NoStoragePattern = new(
        @"sem arrecada[çc][ãa]o|sem arrumos|n[ãa]o (tem|possui|disp[õo]e de) (arrecada[çc][ãa]o|arrumos)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HasStoragePattern = new(
        @"arrecada[çc][ãa]o|\barrumos\b|\barrumo\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool? Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        if (NoStoragePattern.IsMatch(description))
            return false;

        if (HasStoragePattern.IsMatch(description))
            return true;

        return null;
    }
}
