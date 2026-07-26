using System.Text.Json;
using EstateAggregator.Data;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Services;

// Geocodes property locations via OpenStreetMap's free Nominatim API so the
// map can plot real pins instead of a generic country-level view. Nominatim's
// usage policy (https://operations.osmfoundation.org/policies/nominatim/)
// requires: max 1 request/sec, and a real identifying User-Agent — both are
// respected here. Results are cached by LocationString onto every Property
// row that shares it, so a run only geocodes each distinct location once
// rather than once per listing.
public class GeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeocodingService> _logger;

    // Keeps a single run's total geocoding time bounded and stays polite to
    // a free public service shared by many other users.
    private const int MaxLocationsPerRun = 25;
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(1100);

    public GeocodingService(HttpClient httpClient, ILogger<GeocodingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task GeocodeMissingAsync(EstateDbContext db, CancellationToken cancellationToken = default)
    {
        var pendingLocations = await db.Properties
            .Where(p => p.Lat == null && p.LocationString != null)
            .Select(p => p.LocationString!)
            .Distinct()
            .Take(MaxLocationsPerRun)
            .ToListAsync(cancellationToken);

        if (pendingLocations.Count == 0)
            return;

        for (var i = 0; i < pendingLocations.Count; i++)
        {
            if (i > 0)
                await Task.Delay(DelayBetweenRequests, cancellationToken);

            var location = pendingLocations[i];
            var coords = await GeocodeOneAsync(location, cancellationToken);
            if (coords == null)
                continue;

            var (lat, lng) = coords.Value;
            var matching = await db.Properties
                .Where(p => p.LocationString == location && p.Lat == null)
                .ToListAsync(cancellationToken);

            foreach (var property in matching)
            {
                property.Lat = lat;
                property.Lng = lng;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(double Lat, double Lng)?> GeocodeOneAsync(string location, CancellationToken cancellationToken)
    {
        try
        {
            var query = Uri.EscapeDataString($"{location}, Portugal");
            var url = $"https://nominatim.openstreetmap.org/search?q={query}&format=json&limit=1&countrycodes=pt";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim returned {Status} for '{Location}'", response.StatusCode, location);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var first = doc.RootElement[0];
            var lat = double.Parse(first.GetProperty("lat").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            var lon = double.Parse(first.GetProperty("lon").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            return (lat, lon);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geocoding failed for '{Location}'", location);
            return null;
        }
    }
}
