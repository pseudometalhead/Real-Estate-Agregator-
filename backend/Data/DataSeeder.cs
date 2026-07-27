using System.Text.Json;
using EstateAggregator.Models;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Data;

public static class DataSeeder
{
    // Only seeds a default AppSettings row (required for the scraper
    // pipeline to run at all). Demo Properties/MyListings/ScraperRuns seed
    // data was removed once real scraped data replaced it — re-adding
    // placeholder listings on every fresh start would just mix fake rows
    // back into real results.
    public static async Task SeedAsync(EstateDbContext db)
    {
        await SeedAppSettingsAsync(db);
    }

    private static async Task SeedAppSettingsAsync(EstateDbContext db)
    {
        if (await db.AppSettings.AnyAsync())
            return;

        db.AppSettings.Add(new AppSetting
        {
            DistrictsJson = JsonSerializer.Serialize(new[] { "Lisbon", "Porto" }),
            PriceMin = 200000,
            PriceMax = 600000,
            RoomsMin = 1,
            RoomsMax = 4,
            MaxPagesPerSource = 50,
            ScrapeIdealistaEnabled = true,
            ScrapeImoVirtualEnabled = true,
            ScrapeImobiliarioEnabled = true,
            ScrapeCasaSapoEnabled = true,
            ScrapeCaixaImobiliarioEnabled = true,
            ScrapeSantanderEnabled = true,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
