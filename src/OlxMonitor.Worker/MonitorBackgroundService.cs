using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OlxMonitor.Infrastructure.Services;

namespace OlxMonitor.Worker;

public class MonitorBackgroundService : BackgroundService
{
    private readonly OlxScraper _scraper;
    private readonly ILogger<MonitorBackgroundService> _logger;

    public MonitorBackgroundService(OlxScraper scraper, ILogger<MonitorBackgroundService> logger)
    {
        _scraper = scraper;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OLX Monitor Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting scrape cycle...");

                // Your Pixel 10 256 search
                var listings = await _scraper.ScrapeAsync(
                    categoryPath: "/elektronika/telefony/smartfony-telefony-komorkowe/",
                    keyword: "pixel 10 256"
                );

                _logger.LogInformation("Found {Count} listings in this cycle", listings.Count);

                // TODO: Save to DB (next step)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scrape cycle");
            }

            // Wait 5 minutes (configurable later)
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}