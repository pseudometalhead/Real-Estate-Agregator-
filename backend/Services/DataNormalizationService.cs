using System.Globalization;
using EstateAggregator.Utilities;

namespace EstateAggregator.Services;

public class DataNormalizationService
{
    public string NormalizeLocation(string? location) => DedupHashGenerator.NormalizeLocation(location);

    public decimal? NormalizePrice(string? rawPrice)
    {
        if (string.IsNullOrWhiteSpace(rawPrice))
            return null;

        var cleaned = new string(rawPrice.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
        cleaned = cleaned.Replace(".", "").Replace(",", ".");

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
