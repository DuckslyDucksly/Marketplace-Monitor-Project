using Microsoft.EntityFrameworkCore;
using OlxMonitor.Core.Models;

namespace OlxMonitor.Infrastructure.Data;

public class OlxMonitorDbContext : DbContext
{
    public DbSet<Listing> Listings { get; set; } = null!;

    public OlxMonitorDbContext(DbContextOptions<OlxMonitorDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Listing>(entity =>
        {
            entity.HasIndex(l => l.OlxId).IsUnique();           // Prevent duplicates
            entity.HasIndex(l => l.Url).IsUnique();
            
            entity.Property(l => l.Price).HasPrecision(18, 2);
            entity.Property(l => l.ImageUrlsJson).HasColumnType("TEXT");
        });
    }
}