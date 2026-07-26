using System.Security.Cryptography;
using System.Text;

namespace EstateAggregator.Utilities;

public static class DedupHashGenerator
{
    public static string Compute(string? locationString, decimal price, int? beds)
    {
        var normalizedLocation = NormalizeLocation(locationString);
        var hashInput = $"{normalizedLocation}|{price:F0}|{beds ?? 0}";
        return ComputeSha256(hashInput)[..16];
    }

    public static string NormalizeLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return string.Empty;

        var normalized = location.Trim().ToLowerInvariant();
        normalized = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var decomposed = normalized.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
