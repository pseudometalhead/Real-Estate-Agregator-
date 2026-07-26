using System.Text.Json;
using EstateAggregator.Models;
using EstateAggregator.Utilities;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(EstateDbContext db)
    {
        await SeedAppSettingsAsync(db);
        await SeedPropertiesAsync(db);
        await SeedMyListingsAsync(db);
        await SeedScraperRunsAsync(db);
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
            ScrapeIdealistaEnabled = true,
            ScrapeImoVirtualEnabled = true,
            ScrapeImobiliarioEnabled = true,
            ScrapeCasaSapoEnabled = true,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static readonly (string Location, decimal Price, int Beds, int Baths, decimal Size, string Source, string Description)[] SeedProperties =
    {
        ("Lisboa, Príncipe Real", 320000, 2, 1, 75, "Idealista", "Apartamento remodelado com excelente exposição solar, fachada sul, muito luminoso durante todo o dia."),
        ("Lisboa, Alfama", 450000, 3, 2, 110, "ImoVirtual", "Apartamento típico de Alfama, virada a norte, com vista para o castelo."),
        ("Porto, Cedofeita", 210000, 1, 1, 55, "Imobiliario", "T1 em bom estado, perto do metro e de todos os serviços."),
        ("Porto, Foz do Douro", 580000, 4, 3, 180, "Idealista", "Moradia com vista para o rio, exposição poente, terraço amplo."),
        ("Lisboa, Campo de Ourique", 395000, 2, 2, 90, "ImoVirtual", "Apartamento com fachada nascente, muito luminoso pela manhã."),
        ("Porto, Bonfim", 245000, 2, 1, 68, "Imobiliario", "Apartamento remodelado, a sul, próximo do centro histórico."),
        ("Lisboa, Areeiro", 275000, 1, 1, 58, "Idealista", "T1 sem informação de orientação, bem localizado junto ao metro."),
        ("Cascais, Estoril", 560000, 3, 2, 140, "Idealista", "Apartamento de luxo com varanda virada a poente com vista mar."),
        ("Porto, Boavista", 320000, 2, 2, 85, "ImoVirtual", "Apartamento moderno, sol nascente de manhã, muita luz natural."),
        ("Lisboa, Benfica", 230000, 2, 1, 72, "Imobiliario", "T2 virado a norte, junto ao estádio, bom para investimento."),
        ("Lisboa, Parque das Nações", 480000, 3, 2, 120, "Idealista", "Apartamento moderno, frente sul, vista para o rio Tejo."),
        ("Porto, Aliados", 260000, 1, 1, 50, "ImoVirtual", "T1 no centro histórico, sem indicação de orientação solar."),
    };

    private static async Task SeedPropertiesAsync(EstateDbContext db)
    {
        if (await db.Properties.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        var index = 1;

        foreach (var seed in SeedProperties)
        {
            var (orientation, orientationSource) = OrientationExtractor.Extract(seed.Description);

            db.Properties.Add(new Property
            {
                Url = $"https://www.{seed.Source.ToLowerInvariant()}.pt/imovel/demo-{index:000}",
                Source = seed.Source,
                Price = seed.Price,
                LocationString = seed.Location,
                Beds = seed.Beds,
                Baths = seed.Baths,
                SizeM2 = seed.Size,
                Description = seed.Description,
                SunOrientation = orientation,
                OrientationSource = orientationSource,
                PhotosJson = "[]",
                SourcePropertyId = $"demo-{index:000}",
                DedupHash = DedupHashGenerator.Compute(seed.Location, seed.Price, seed.Beds),
                CreatedAt = now,
                LastSeenAt = now,
                FirstScrapedAt = now
            });

            index++;
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedMyListingsAsync(EstateDbContext db)
    {
        if (await db.MyListings.AnyAsync())
            return;

        var properties = await db.Properties.OrderBy(p => p.Id).ToListAsync();
        if (properties.Count < 12)
            return;

        var now = DateTime.UtcNow;

        db.MyListings.AddRange(
            new MyListing
            {
                PropertyId = properties[0].Id,
                Status = "Interested",
                Notes = "Ótima localização, marcar visita.",
                AgentName = "Ana Ferreira",
                AgentPhone = "+351 912 345 678",
                DateAdded = now,
                LastUpdated = now
            },
            new MyListing
            {
                PropertyId = properties[3].Id,
                Status = "Contacted",
                Notes = "Perguntei sobre a orientação solar exata, à espera de resposta.",
                AgentName = "Ricardo Sousa",
                AgentPhone = "+351 913 555 222",
                AskedAboutOrientation = true,
                FollowUpDate = now.AddDays(3),
                DateAdded = now.AddDays(-2),
                LastUpdated = now
            },
            new MyListing
            {
                PropertyId = properties[7].Id,
                Status = "Waiting",
                Notes = "À espera de resposta do agente sobre condomínio.",
                AgentName = "Marta Lima",
                DateAdded = now.AddDays(-1),
                LastUpdated = now
            },
            new MyListing
            {
                PropertyId = properties[11].Id,
                Status = "Rejected",
                Notes = "Sem orientação solar e sem elevador, não é boa opção.",
                DateAdded = now.AddDays(-5),
                LastUpdated = now.AddDays(-4)
            }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedScraperRunsAsync(EstateDbContext db)
    {
        if (await db.ScraperRuns.AnyAsync())
            return;

        // Comfortably inside the daily report's "last 24h" window (not exactly
        // -24h, which would be excluded depending on the few seconds/minutes
        // that pass between seeding and the report being requested).
        var recentRunTime = DateTime.UtcNow.AddHours(-2);
        var sources = new[] { "Idealista", "ImoVirtual", "Imobiliario", "CasaSapo" };

        foreach (var source in sources)
        {
            db.ScraperRuns.Add(new ScraperRun
            {
                Source = source,
                RunStartTime = recentRunTime,
                RunEndTime = recentRunTime.AddSeconds(1),
                PropertiesFound = 0,
                PropertiesAdded = 0,
                PropertiesUpdated = 0,
                PropertiesSkipped = 0,
                HasErrors = true,
                ErrorsJson = JsonSerializer.Serialize(new[] { "Integration not yet configured" }),
                DurationSeconds = 1
            });
        }

        await db.SaveChangesAsync();
    }
}
