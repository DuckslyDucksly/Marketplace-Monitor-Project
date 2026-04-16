using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
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
                // Better path: puts the database next to the project (not deep in bin/Debug)
                var dbFolder = Path.Combine(Directory.GetCurrentDirectory(), "Data");
                Directory.CreateDirectory(dbFolder);
                var dbPath = Path.Combine(dbFolder, "olxmonitor.db");

                services.AddDbContext<OlxMonitorDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath};Cache=Shared"));

                services.AddHttpClient();
                services.AddSingleton<OlxScraper>();
                services.AddHostedService<MonitorBackgroundService>();

                Log.Information("Using database: {DbPath}", dbPath);
            })
            .Build();

        // Apply migrations on startup
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OlxMonitorDbContext>();
            await db.Database.MigrateAsync();
            Log.Information("✅ Database ready");
        }

        Log.Information("🚀 OLX Monitor starting...");
        await host.RunAsync();
    }
}