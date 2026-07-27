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
            Distrito = p.Distrito,
            Concelho = p.Concelho,
            Freguesia = p.Freguesia,
            Lat = p.Lat,
            Lng = p.Lng,
            Beds = p.Beds,
            Baths = p.Baths,
            SizeM2 = p.SizeM2,
            Description = p.Description,
            SunOrientation = p.SunOrientation,
            OrientationSource = p.OrientationSource,
            OpenPlanKitchen = p.OpenPlanKitchen,
            ConstructionStatus = p.ConstructionStatus,
            Elevator = p.Elevator,
            Parking = p.Parking,
            Furnished = p.Furnished,
            AirConditioning = p.AirConditioning,
            Balcony = p.Balcony,
            Renovated = p.Renovated,
            Storage = p.Storage,
            WaterView = p.WaterView,
            NearMetro = p.NearMetro,
            HasUsageLicense = p.HasUsageLicense,
            EnergyRating = p.EnergyRating,
            Floor = p.Floor,
            TotalFloors = p.TotalFloors,
            CondoFeeMonthly = p.CondoFeeMonthly,
            HasPool = p.HasPool,
            HasGarden = p.HasGarden,
            YearBuilt = p.YearBuilt,
            AiEnrichedAt = p.AiEnrichedAt,
            ExternalId = p.SourcePropertyId,
            AgentName = p.AgentName,
            AgentPhone = p.AgentPhone,
            AgentEmail = p.AgentEmail,
            Photos = DeserializeStringList(p.PhotosJson),
            CreatedAt = p.CreatedAt,
            LastSeenAt = p.LastSeenAt,
            PreviousPrice = latest?.OldPrice,
            PriceChangedAt = latest?.ChangedAt,
            PriceHistory = history,
            LinkedSources = p.LinkedSources
                .OrderBy(s => s.Source)
                .Select(s => new PropertySourceDto { Source = s.Source, Url = s.Url, Price = s.Price, LastSeenAt = s.LastSeenAt })
                .ToList()
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
        AskedAboutOpenPlanKitchen = l.AskedAboutOpenPlanKitchen,
        FollowUpDate = l.FollowUpDate,
        DateAdded = l.DateAdded,
        LastUpdated = l.LastUpdated,
        CommHistoryCount = l.CommHistory.Count,
        LastContactedAt = l.CommHistory.Count > 0 ? l.CommHistory.Max(c => c.CreatedAt) : null
    };

    public static CommHistoryEntryDto ToDto(this CommHistoryEntry c) => new()
    {
        Id = c.Id,
        MyListingId = c.MyListingId,
        Channel = c.Channel,
        Direction = c.Direction,
        Subject = c.Subject,
        Message = c.Message,
        CreatedAt = c.CreatedAt
    };

    public static AppSettingDto ToDto(this AppSetting s) => new()
    {
        Districts = DeserializeStringList(s.DistrictsJson),
        PriceMin = s.PriceMin,
        PriceMax = s.PriceMax,
        RoomsMin = s.RoomsMin,
        RoomsMax = s.RoomsMax,
        MaxPagesPerSource = s.MaxPagesPerSource,
        ScrapeIdealistaEnabled = s.ScrapeIdealistaEnabled,
        ScrapeImoVirtualEnabled = s.ScrapeImoVirtualEnabled,
        ScrapeImobiliarioEnabled = s.ScrapeImobiliarioEnabled,
        ScrapeCasaSapoEnabled = s.ScrapeCasaSapoEnabled,
        ScrapeCaixaImobiliarioEnabled = s.ScrapeCaixaImobiliarioEnabled,
        ScrapeSantanderEnabled = s.ScrapeSantanderEnabled,
        AvailabilityText = s.AvailabilityText,
        SenderName = s.SenderName,
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
        PropertiesLinked = r.PropertiesLinked,
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
