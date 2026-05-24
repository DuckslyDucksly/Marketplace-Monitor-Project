using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<MonitorSettings>>().Value;

        _logger.LogInformation("🔍 Starting scrape cycle with {SearchCount} configured searches...", 
            settings.Searches.Count);

        foreach (var search in settings.Searches)
        {
            try
            {
                _logger.LogInformation("Scraping for: {Keyword}", search.Keyword);

                var listings = await _scraper.ScrapeAsync(
                    search.CategoryPath, 
                    search.Keyword, 
                    maxPages: 2);

                int added = 0, updated = 0, skipped = 0;

                // Track URLs/OlxIds seen in current batch to avoid duplicate inserts before SaveChanges
                var seenOlxIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var listing in listings)
                {
                    if (string.IsNullOrWhiteSpace(listing.OlxId) || string.IsNullOrWhiteSpace(listing.Url))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        // Skip if duplicate within current batch
                        if (seenOlxIds.Contains(listing.OlxId) || seenUrls.Contains(listing.Url))
                        {
                            skipped++;
                            continue;
                        }

                        // Stronger duplicate check against DB
                        var existing = await db.Listings
                            .FirstOrDefaultAsync(l => l.OlxId == listing.OlxId || l.Url == listing.Url, stoppingToken);

                        if (existing == null)
                        {
                            db.Listings.Add(listing);
                            seenOlxIds.Add(listing.OlxId);
                            seenUrls.Add(listing.Url);
                            added++;
                            _logger.LogWarning("🆕 NEW: {Title} | {Price} zł | {Location}", 
                                listing.Title, listing.Price, listing.Location);
                        }
                        else
                        {
                            seenOlxIds.Add(listing.OlxId);
                            seenUrls.Add(listing.Url);
                            // Always update LastSeen
                            existing.LastSeen = DateTime.UtcNow;

                            // Update other fields if changed
                            if (existing.Title != listing.Title) existing.Title = listing.Title;
                            if (existing.Price != listing.Price) existing.Price = listing.Price;
                            if (existing.Location != listing.Location) existing.Location = listing.Location;

                            updated++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process listing {OlxId} / {Url}", listing.OlxId, listing.Url);
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
