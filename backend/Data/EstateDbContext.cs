using EstateAggregator.Models;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Data;

public class EstateDbContext : DbContext
{
    public EstateDbContext(DbContextOptions<EstateDbContext> options) : base(options)
    {
    }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<MyListing> MyListings => Set<MyListing>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ScraperRun> ScraperRuns => Set<ScraperRun>();
    public DbSet<PriceHistoryEntry> PriceHistoryEntries => Set<PriceHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Property>(entity =>
        {
            entity.HasIndex(p => p.Url).IsUnique();
            entity.HasIndex(p => p.Source);
            entity.HasIndex(p => p.DedupHash);
            entity.HasIndex(p => p.Price);
            entity.HasIndex(p => p.LocationString);
            entity.HasIndex(p => p.Beds);
            entity.HasIndex(p => p.LastSeenAt);

            entity.Property(p => p.Price).HasColumnType("decimal(10,2)");
            entity.Property(p => p.SizeM2).HasColumnType("decimal(8,2)");
        });

        modelBuilder.Entity<MyListing>(entity =>
        {
            entity.HasIndex(l => l.Status);
            entity.HasIndex(l => l.PropertyId).IsUnique();

            entity.HasOne(l => l.Property)
                .WithOne(p => p.MyListing)
                .HasForeignKey<MyListing>(l => l.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScraperRun>(entity =>
        {
            entity.HasIndex(r => r.Source);
            entity.HasIndex(r => r.RunStartTime);
        });

        modelBuilder.Entity<PriceHistoryEntry>(entity =>
        {
            entity.HasIndex(e => e.PropertyId);
            entity.Property(e => e.OldPrice).HasColumnType("decimal(10,2)");
            entity.Property(e => e.NewPrice).HasColumnType("decimal(10,2)");

            entity.HasOne(e => e.Property)
                .WithMany(p => p.PriceHistory)
                .HasForeignKey(e => e.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.Property(s => s.PriceMin).HasColumnType("decimal(10,2)");
            entity.Property(s => s.PriceMax).HasColumnType("decimal(10,2)");
        });
    }
}
