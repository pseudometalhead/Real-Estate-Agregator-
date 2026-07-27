using System.Text.Json;
using EstateAggregator.Data;
using EstateAggregator.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Services;

public class AppSettingsService
{
    private readonly EstateDbContext _db;

    public AppSettingsService(EstateDbContext db)
    {
        _db = db;
    }

    public async Task<AppSettingDto?> GetAsync()
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync();
        return settings?.ToDto();
    }

    public async Task<AppSettingDto?> UpdateAsync(UpdateAppSettingDto dto)
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync();
        if (settings == null)
            return null;

        settings.DistrictsJson = JsonSerializer.Serialize(dto.Districts);
        settings.PriceMin = dto.PriceMin;
        settings.PriceMax = dto.PriceMax;
        settings.RoomsMin = dto.RoomsMin;
        settings.RoomsMax = dto.RoomsMax;
        settings.MaxPagesPerSource = dto.MaxPagesPerSource;
        settings.ScrapeIdealistaEnabled = dto.ScrapeIdealistaEnabled;
        settings.ScrapeImoVirtualEnabled = dto.ScrapeImoVirtualEnabled;
        settings.ScrapeImobiliarioEnabled = dto.ScrapeImobiliarioEnabled;
        settings.ScrapeCasaSapoEnabled = dto.ScrapeCasaSapoEnabled;
        settings.ScrapeCaixaImobiliarioEnabled = dto.ScrapeCaixaImobiliarioEnabled;
        settings.ScrapeSantanderEnabled = dto.ScrapeSantanderEnabled;
        settings.AvailabilityText = dto.AvailabilityText;
        settings.SenderName = dto.SenderName;
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return settings.ToDto();
    }
}
