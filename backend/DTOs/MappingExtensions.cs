using System.Text.Json;
using EstateAggregator.Models;

namespace EstateAggregator.DTOs;

public static class MappingExtensions
{
    public static PropertyDto ToDto(this Property p)
    {
        var history = p.PriceHistory
            .OrderBy(h => h.ChangedAt)
            .Select(h => new PriceHistoryPointDto { OldPrice = h.OldPrice, NewPrice = h.NewPrice, ChangedAt = h.ChangedAt })
            .ToList();
        var latest = history.LastOrDefault();

        return new PropertyDto
        {
            Id = p.Id,
            Url = p.Url,
            Source = p.Source,
            Price = p.Price,
            Location = p.LocationString,
            Lat = p.Lat,
            Lng = p.Lng,
            Beds = p.Beds,
            Baths = p.Baths,
            SizeM2 = p.SizeM2,
            Description = p.Description,
            SunOrientation = p.SunOrientation,
            OrientationSource = p.OrientationSource,
            Photos = DeserializeStringList(p.PhotosJson),
            CreatedAt = p.CreatedAt,
            LastSeenAt = p.LastSeenAt,
            PreviousPrice = latest?.OldPrice,
            PriceChangedAt = latest?.ChangedAt,
            PriceHistory = history
        };
    }

    public static MyListingDto ToDto(this MyListing l) => new()
    {
        Id = l.Id,
        PropertyId = l.PropertyId,
        Property = l.Property?.ToDto(),
        Status = l.Status,
        Notes = l.Notes,
        AgentName = l.AgentName,
        AgentPhone = l.AgentPhone,
        AgentEmail = l.AgentEmail,
        AskedAboutOrientation = l.AskedAboutOrientation,
        FollowUpDate = l.FollowUpDate,
        DateAdded = l.DateAdded,
        LastUpdated = l.LastUpdated
    };

    public static AppSettingDto ToDto(this AppSetting s) => new()
    {
        Districts = DeserializeStringList(s.DistrictsJson),
        PriceMin = s.PriceMin,
        PriceMax = s.PriceMax,
        RoomsMin = s.RoomsMin,
        RoomsMax = s.RoomsMax,
        ScrapeIdealistaEnabled = s.ScrapeIdealistaEnabled,
        ScrapeImoVirtualEnabled = s.ScrapeImoVirtualEnabled,
        ScrapeImobiliarioEnabled = s.ScrapeImobiliarioEnabled,
        ScrapeCasaSapoEnabled = s.ScrapeCasaSapoEnabled,
        LastScrapedAt = s.LastScrapedAt
    };

    public static ScraperRunDto ToDto(this ScraperRun r) => new()
    {
        Id = r.Id,
        Source = r.Source,
        RunStartTime = r.RunStartTime,
        RunEndTime = r.RunEndTime,
        PropertiesFound = r.PropertiesFound,
        PropertiesAdded = r.PropertiesAdded,
        PropertiesUpdated = r.PropertiesUpdated,
        PropertiesSkipped = r.PropertiesSkipped,
        HasErrors = r.HasErrors,
        Errors = DeserializeStringList(r.ErrorsJson),
        DurationSeconds = r.DurationSeconds
    };

    public static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
}
