using EstateAggregator.Data;
using EstateAggregator.Models;
using EstateAggregator.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Tests;

// A one-time backfill (BackfillLocationHierarchyAsync) mislabeled Distrito/
// Concelho for every pre-existing property by naively splitting the flat
// LocationString in half — the second half is usually a freguesia/
// neighborhood name, not a concelho (e.g. "Pombeiro de Ribavizela" landed in
// Concelho when it's actually a parish of Felgueiras). Every scraper's own
// MapToProperty already derives Distrito/Concelho/Freguesia correctly from
// the source's real structured fields, so a re-scrape should overwrite the
// bad backfilled values — but DeduplicationService's URL-match ("Updated")
// path never touched these three fields, silently keeping the wrong data
// forever even after a fresh, correct re-scrape. These tests pin the fix.
public class DeduplicationServiceLocationRefreshTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly EstateDbContext _db;
    private readonly DeduplicationService _service = new();

    public DeduplicationServiceLocationRefreshTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<EstateDbContext>().UseSqlite(_connection).Options;
        _db = new EstateDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ReScrapeMatchingUrl_OverwritesStaleLocationHierarchy()
    {
        _db.Properties.Add(new Property
        {
            Url = "https://example.com/listing/1",
            Source = "Idealista",
            LocationString = "Porto, Pombeiro de Ribavizela",
            Distrito = "Porto",
            Concelho = "Pombeiro de Ribavizela", // wrong: this is a freguesia, not a concelho
            Freguesia = null,
            DedupHash = "hash-1",
        });
        await _db.SaveChangesAsync();

        var candidate = new Property
        {
            Url = "https://example.com/listing/1",
            Source = "Idealista",
            LocationString = "Porto, Felgueiras, Pombeiro de Ribavizela",
            Distrito = "Porto",
            Concelho = "Felgueiras", // correct, from the source's own Municipality field
            Freguesia = "Pombeiro de Ribavizela",
            DedupHash = "hash-1",
        };

        var outcome = await _service.ProcessAsync(_db, candidate);
        await _db.SaveChangesAsync();

        Assert.Equal(DedupOutcome.Updated, outcome);
        var updated = await _db.Properties.FirstAsync(p => p.Url == "https://example.com/listing/1");
        Assert.Equal("Felgueiras", updated.Concelho);
        Assert.Equal("Pombeiro de Ribavizela", updated.Freguesia);
    }

    // Idealista's own API isn't internally consistent about what
    // "Municipality" means (verified live) — sometimes it returns a
    // freguesia name there instead of the real concelho, so even a
    // freshly-scraped candidate (not stale data) can arrive with a
    // misplaced Concelho. This must self-correct on every scrape, not just
    // via a one-off backfill, since the automatic twice-daily cron re-scrape
    // hits the same upstream inconsistency every time it runs.
    [Fact]
    public async Task NewProperty_WithMisplacedConcelhoFromSource_IsCorrectedOnInsert()
    {
        var candidate = new Property
        {
            Url = "https://example.com/listing/2",
            Source = "Idealista",
            LocationString = "Porto, Arcozelo",
            Distrito = "Porto",
            Concelho = "Arcozelo", // Idealista's Municipality field returned a freguesia, not the real concelho
            Freguesia = "Some Neighborhood",
            DedupHash = "hash-2",
        };

        var outcome = await _service.ProcessAsync(_db, candidate);
        await _db.SaveChangesAsync();

        Assert.Equal(DedupOutcome.Added, outcome);
        var added = await _db.Properties.FirstAsync(p => p.Url == "https://example.com/listing/2");
        Assert.Equal("Vila Nova de Gaia", added.Concelho);
        // A legitimate, more-specific Freguesia value already present must
        // not be clobbered by the demoted (coarser) parish name.
        Assert.Equal("Some Neighborhood", added.Freguesia);
    }

    [Fact]
    public async Task ReScrapeMatchingUrl_WithMisplacedConcelhoFromSource_IsCorrectedOnUpdate()
    {
        _db.Properties.Add(new Property
        {
            Url = "https://example.com/listing/3",
            Source = "Idealista",
            LocationString = "Porto, Vila Nova de Gaia",
            Distrito = "Porto",
            Concelho = "Vila Nova de Gaia",
            Freguesia = "Oliveira do Douro",
            DedupHash = "hash-3",
        });
        await _db.SaveChangesAsync();

        // A later re-scrape happens to hit Idealista's inconsistent
        // Municipality field for this same listing.
        var candidate = new Property
        {
            Url = "https://example.com/listing/3",
            Source = "Idealista",
            LocationString = "Porto, Oliveira do Douro",
            Distrito = "Porto",
            Concelho = "Oliveira do Douro",
            Freguesia = null,
            DedupHash = "hash-3",
        };

        await _service.ProcessAsync(_db, candidate);
        await _db.SaveChangesAsync();

        var updated = await _db.Properties.FirstAsync(p => p.Url == "https://example.com/listing/3");
        Assert.Equal("Vila Nova de Gaia", updated.Concelho);
    }
}
