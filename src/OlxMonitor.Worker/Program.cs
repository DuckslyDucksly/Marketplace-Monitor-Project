using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OlxMonitor.Core.Models;
using OlxMonitor.Infrastructure.Data;
using OlxMonitor.Infrastructure.Services;
using Serilog;
using Spectre.Console;

namespace OlxMonitor.Worker;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        // Viewer command: dotnet run -- viewer
        if (args.Length > 0 && args[0].Equals("viewer", StringComparison.OrdinalIgnoreCase))
        {
            await RunViewerAsync();
            return;
        }

        // Normal monitor mode
        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                var dbFolder = Path.Combine(Directory.GetCurrentDirectory(), "Data");
                Directory.CreateDirectory(dbFolder);
                var dbPath = Path.Combine(dbFolder, "olxmonitor.db");

                services.AddDbContext<OlxMonitorDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath};Cache=Shared"));

                services.Configure<MonitorSettings>(context.Configuration.GetSection("MonitorSettings"));

                services.AddHttpClient();
                services.AddSingleton<OlxScraper>();
                services.AddHostedService<MonitorBackgroundService>();

                Log.Information("Using database: {DbPath}", dbPath);
            })
            .Build();

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OlxMonitorDbContext>();
            await db.Database.MigrateAsync();
            Log.Information("✅ Database ready");
        }

        Log.Information("🚀 OLX Monitor starting...");
        await host.RunAsync();
    }

    private static async Task RunViewerAsync()
    {
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "olxmonitor.db");

        if (!File.Exists(dbPath))
        {
            AnsiConsole.MarkupLine("[red]❌ Database not found:[/] {0}", dbPath);
            return;
        }

        await using var db = new OlxMonitorDbContext(
            new DbContextOptionsBuilder<OlxMonitorDbContext>()
                .UseSqlite($"Data Source={dbPath};Cache=Shared")
                .Options);

        var listings = await db.Listings
            .OrderByDescending(l => l.FirstSeen)
            .Take(100)
            .ToListAsync();

        if (listings.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No listings found yet.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[green]OLX Monitor - Saved Listings[/]")
            .AddColumn("[bold]ID[/]")
            .AddColumn("[bold]Title[/]")
            .AddColumn("[bold]Price[/]")
            .AddColumn("[bold]Location[/]")
            .AddColumn("[bold]First Seen[/]")
            .AddColumn("[bold]Last Seen[/]")
            .AddColumn("[bold]Url[/]");

        foreach (var l in listings)
        {
            table.AddRow(
                l.Id.ToString(),
                l.Title.Length > 65 ? l.Title.Substring(0, 62) + "..." : l.Title,
                $"[cyan]{l.Price:0.00} zł[/]",
                l.Location,
                l.FirstSeen.ToString("yyyy-MM-dd HH:mm"),
                l.LastSeen.ToString("yyyy-MM-dd HH:mm"),
                l.Url.Length > 55 ? l.Url.Substring(0, 52) + "..." : l.Url
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[green]Showing {listings.Count} newest listings.[/]");
        AnsiConsole.MarkupLine("[gray]Run with -- viewer to see this again.[/]");
    }
}