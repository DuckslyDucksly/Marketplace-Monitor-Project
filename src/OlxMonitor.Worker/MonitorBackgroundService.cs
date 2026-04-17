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
        _logger.LogInformation("🚀 OLX Monitor is now ACTIVE - checking configured searches every 5 minutes");

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

        _logger.LogInformation("🔍 Starting scrape cycle...");

        // Simple hardcoded searches for now (easy to extend later)
        var searches = new[]
        {
            new { CategoryPath = "/elektronika/komputery/podzespoly-i-czesci/", Keyword = "ddr4 ram" }
            // Add more searches here if you want:
            // new { CategoryPath = "/elektronika/telefony/smartfony-telefony-komorkowe/", Keyword = "pixel 10" }
        };

        foreach (var search in searches)
        {
            try
            {
                _logger.LogInformation("Scraping for: {Keyword}", search.Keyword);

                var listings = await _scraper.ScrapeAsync(
                    search.CategoryPath, 
                    search.Keyword, 
                    maxPages: 2);

                int added = 0, updated = 0, skipped = 0;

                foreach (var listing in listings)
                {
                    if (string.IsNullOrWhiteSpace(listing.OlxId) || string.IsNullOrWhiteSpace(listing.Url))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        // Check BOTH OlxId and Url to prevent UNIQUE constraint errors
                        var existing = await db.Listings
                            .FirstOrDefaultAsync(l => l.OlxId == listing.OlxId || l.Url == listing.Url, stoppingToken);

                        if (existing == null)
                        {
                            db.Listings.Add(listing);
                            added++;
                            _logger.LogWarning("🆕 NEW: {Title} | {Price} zł | {Location}", 
                                listing.Title, listing.Price, listing.Location);
                        }
                        else
                        {
                            // Update last seen time and any changed fields
                            existing.LastSeen = DateTime.UtcNow;

                            if (existing.Title != listing.Title || 
                                existing.Price != listing.Price || 
                                existing.Location != listing.Location)
                            {
                                existing.Title = listing.Title;
                                existing.Price = listing.Price;
                                existing.Location = listing.Location;
                            }
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping {Keyword}", search.Keyword);
            }
        }
    }
}