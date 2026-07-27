using System.Security.Cryptography;
using System.Text;

namespace EstateAggregator.Utilities;

public static class DedupHashGenerator
{
    // Prices are bucketed to the nearest 2500 (rather than hashed exactly)
    // before hashing so the same physical apartment listed on two different
    // portals — where a small €500-2000 discrepancy from rounding or fees is
    // common — still lands in the same hash and is recognized as a
    // cross-portal duplicate, instead of being treated as two properties.
    private const decimal PriceBucketSize = 2500m;

    // Found live: location+price+beds alone is too coarse for new
    // developments, where a builder lists many genuinely distinct units at
    // the same starting price in the same building (e.g. "Monte da Virgem
    // Flats desde 262.500" — five different T2s, same price, same
    // neighborhood). Without size in the hash these all collided into one
    // "property" with the other four silently merged in as if they were
    // re-listings of the same unit, hiding four real, available apartments.
    // Size is bucketed to the nearest whole m² — tight enough to tell
    // distinct units apart, loose enough to absorb rounding differences
    // between portals (e.g. 66 vs 66.4).
    private const decimal SizeBucketSize = 1m;

    public static string Compute(string? locationString, decimal price, int? beds, decimal? sizeM2 = null)
    {
        var normalizedLocation = NormalizeLocation(locationString);
        var bucketedPrice = Math.Round(price / PriceBucketSize) * PriceBucketSize;
        var bucketedSize = sizeM2.HasValue ? Math.Round(sizeM2.Value / SizeBucketSize) * SizeBucketSize : (decimal?)null;
        var hashInput = $"{normalizedLocation}|{bucketedPrice:F0}|{beds ?? 0}|{(bucketedSize.HasValue ? bucketedSize.Value.ToString("F0") : "?")}";
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
