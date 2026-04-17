using Microsoft.EntityFrameworkCore;
using OlxMonitor.Core.Models;
using OlxMonitor.Infrastructure.Data;
using Xunit;
using FluentAssertions;
using System.IO;

namespace OlxMonitor.Tests;

public class DbContextTests
{
    private async Task<OlxMonitorDbContext> CreateDbContextAsync()
    {
    var dbPath = Path.GetTempFileName();

    var options = new DbContextOptionsBuilder<OlxMonitorDbContext>()
        .UseSqlite($"Data Source={dbPath};Cache=Shared", b => b.MigrationsAssembly("OlxMonitor.Infrastructure"))
        .Options;

    var db = new OlxMonitorDbContext(options);
    await db.Database.MigrateAsync();   // Applies migrations to create Listings table
    return db;
    }

    [Fact]
    public async Task AddListing_ShouldPreventDuplicates_ByOlxIdAndUrl()
    {
        await using var db = await CreateDbContextAsync();

        var listing1 = new Listing 
        { 
            OlxId = "ID123", 
            Url = "https://olx.pl/oferta/123", 
            Title = "Test", 
            Price = 1000, 
            Location = "Kraków",
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            PostedDate = DateTime.UtcNow
        };

        db.Listings.Add(listing1);
        await db.SaveChangesAsync();

        // Try to add duplicate
        var listing2 = new Listing 
        { 
            OlxId = "ID123", 
            Url = "https://olx.pl/oferta/123", 
            Title = "Different", 
            Price = 999 
        };

        await Assert.ThrowsAsync<DbUpdateException>(async () => 
        {
            db.Listings.Add(listing2);
            await db.SaveChangesAsync();
        });
    }
}