using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OlxMonitor.Infrastructure.Data;
using Serilog;
using OlxMonitor.Infrastructure.Services;

namespace OlxMonitor.Worker;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // SQLite with persistent file (Docker volume will mount /app/data)
                services.AddDbContext<OlxMonitorDbContext>(options =>
                    options.UseSqlite("Data Source=/app/data/olxmonitor.db;Cache=Shared"));

                // Register services (we'll add Scraper + Worker next)
                services.AddHttpClient();
                services.AddSingleton<OlxScraper>();           // we'll create this next
                services.AddHostedService<MonitorBackgroundService>();
            })
            .Build();

        // Apply migrations
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OlxMonitorDbContext>();
            await db.Database.MigrateAsync();
            Log.Information("Database ready");
        }

        Log.Information("🚀 OLX Monitor starting...");
        await host.RunAsync();
    }
}