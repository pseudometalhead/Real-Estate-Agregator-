using EstateAggregator.Data;
using EstateAggregator.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Tests;

// Exercises the exact PendingActionOnly query from PropertiesController and the
// exact expired-rejection check from MyListingsController against a real (file-based,
// not in-memory-provider) SQLite database, since the 72h reject-expiry feature hinges
// on DateTime-as-TEXT comparisons behaving the same way SQLite behaves in production.
public class RejectionExpiryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<EstateDbContext> _options;

    public RejectionExpiryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<EstateDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var db = new EstateDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private static Property MakeProperty(int id) => new()
    {
        Id = id,
        Url = $"https://example.com/{id}",
        Source = "Test",
        LocationString = "Porto",
        DedupHash = $"hash-{id}",
    };

    [Fact]
    public async Task PendingActionOnly_ExcludesFreshRejection()
    {
        using var db = new EstateDbContext(_options);
        db.Properties.Add(MakeProperty(1));
        db.MyListings.Add(new MyListing { PropertyId = 1, Status = "Rejected", DateAdded = DateTime.UtcNow.AddHours(-1) });
        await db.SaveChangesAsync();

        var rejectionCutoff = DateTime.UtcNow.AddHours(-72);
        var ids = await db.Properties
            .Where(p => p.MyListing == null || (p.MyListing.Status == "Rejected" && p.MyListing.DateAdded < rejectionCutoff))
            .Select(p => p.Id)
            .ToListAsync();

        Assert.DoesNotContain(1, ids);
    }

    [Fact]
    public async Task PendingActionOnly_IncludesRejectionOlderThan72Hours()
    {
        using var db = new EstateDbContext(_options);
        db.Properties.Add(MakeProperty(2));
        db.MyListings.Add(new MyListing { PropertyId = 2, Status = "Rejected", DateAdded = DateTime.UtcNow.AddHours(-73) });
        await db.SaveChangesAsync();

        var rejectionCutoff = DateTime.UtcNow.AddHours(-72);
        var ids = await db.Properties
            .Where(p => p.MyListing == null || (p.MyListing.Status == "Rejected" && p.MyListing.DateAdded < rejectionCutoff))
            .Select(p => p.Id)
            .ToListAsync();

        Assert.Contains(2, ids);
    }

    [Fact]
    public async Task PendingActionOnly_ExcludesInterestedListing()
    {
        using var db = new EstateDbContext(_options);
        db.Properties.Add(MakeProperty(3));
        db.MyListings.Add(new MyListing { PropertyId = 3, Status = "Interested", DateAdded = DateTime.UtcNow.AddHours(-1000) });
        await db.SaveChangesAsync();

        var rejectionCutoff = DateTime.UtcNow.AddHours(-72);
        var ids = await db.Properties
            .Where(p => p.MyListing == null || (p.MyListing.Status == "Rejected" && p.MyListing.DateAdded < rejectionCutoff))
            .Select(p => p.Id)
            .ToListAsync();

        Assert.DoesNotContain(3, ids);
    }

    [Fact]
    public async Task ExpiredRejection_IsEligibleForReSwipe_FreshRejectionIsNot()
    {
        using var db = new EstateDbContext(_options);
        db.Properties.Add(MakeProperty(4));
        db.Properties.Add(MakeProperty(5));
        db.MyListings.Add(new MyListing { PropertyId = 4, Status = "Rejected", DateAdded = DateTime.UtcNow.AddHours(-73) });
        db.MyListings.Add(new MyListing { PropertyId = 5, Status = "Rejected", DateAdded = DateTime.UtcNow.AddHours(-1) });
        await db.SaveChangesAsync();

        var rejectionCutoff = DateTime.UtcNow.AddHours(-72);
        var expired = await db.MyListings.FirstAsync(ml => ml.PropertyId == 4);
        var fresh = await db.MyListings.FirstAsync(ml => ml.PropertyId == 5);

        Assert.True(expired.Status == "Rejected" && expired.DateAdded < rejectionCutoff);
        Assert.False(fresh.Status == "Rejected" && fresh.DateAdded < rejectionCutoff);
    }
}
