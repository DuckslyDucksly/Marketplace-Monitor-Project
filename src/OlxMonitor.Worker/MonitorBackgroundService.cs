using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OlxMonitor.Core.Models;
using OlxMonitor.Infrastructure.Data;
using OlxMonitor.Infrastructure.Services;

namespace OlxMonitor.Worker;

public class MonitorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonitorBackgroundService> _logger;
    private readonly OlxScraper _scraper;

    public MonitorBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<MonitorBackgroundService> logger,
        OlxScraper scraper)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _scraper = scraper;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 OLX Monitor is now ACTIVE - checking for 'pixel 10 256' every 5 minutes");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformScrapeCycle(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in scrape cycle");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task PerformScrapeCycle(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OlxMonitorDbContext>();

        _logger.LogInformation("🔍 Starting scrape cycle for Pixel 10 256...");

        var listings = await _scraper.ScrapeAsync(
            "/elektronika/telefony/smartfony-telefony-komorkowe/",
            "pixel 10 256",
            maxPages: 2
        );

        int added = 0;
        int updated = 0;
        int skipped = 0;

        foreach (var listing in listings)
        {
            if (string.IsNullOrWhiteSpace(listing.OlxId))
            {
                skipped++;
                continue;
            }

            try
            {
                var existing = await db.Listings
                    .FirstOrDefaultAsync(l => l.OlxId == listing.OlxId, stoppingToken);

                if (existing == null)
                {
                    db.Listings.Add(listing);
                    added++;
                    _logger.LogWarning("🆕 NEW LISTING FOUND: {Title} | {Price} zł | {Location}", 
                        listing.Title, listing.Price, listing.Location);
                }
                else
                {
                    existing.LastSeen = DateTime.UtcNow;
                    updated++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process listing {OlxId}", listing.OlxId);
                skipped++;
            }
        }

        await db.SaveChangesAsync(stoppingToken);

        _logger.LogInformation("✅ Cycle completed. Added: {Added} | Updated: {Updated} | Skipped: {Skipped} | Total in DB: {Total}", 
            added, updated, skipped, await db.Listings.CountAsync(stoppingToken));
    }
}