using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OlxMonitor.Core.Models;
using OlxMonitor.Infrastructure.Data;
using OlxMonitor.Infrastructure.Services;
using Serilog;
using System.Text.RegularExpressions;

namespace OlxMonitor.Worker;

public class Program
{
    private static readonly string AppSettingsPath = "appsettings.json";

    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        if (args.Length > 0 && args[0].Equals("add-url", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: dotnet run -- add-url <full-olx-url>");
                return;
            }

            AddUrl(args[1]);
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

                // ←←← ADD THIS LINE HERE
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

    private static void AddUrl(string url)
    {
        try
        {
            var match = Regex.Match(url, @"olx\.pl(.*?)/q-([^/]+)");
            if (!match.Success)
            {
                Console.WriteLine("Invalid OLX URL. Example: https://www.olx.pl/elektronika/.../q-ddr4-ram/");
                return;
            }

            string categoryPath = match.Groups[1].Value + "/";
            string keyword = match.Groups[2].Value.Replace("-", " ");

            // Load existing config or create new
            var config = new
            {
                MonitorSettings = new
                {
                    Searches = new[]
                    {
                        new { CategoryPath = categoryPath, Keyword = keyword }
                    }
                }
            };

            File.WriteAllText(AppSettingsPath, JsonConvert.SerializeObject(config, Formatting.Indented));

            Console.WriteLine($"✅ Successfully added new search → Category: {categoryPath} | Keyword: {keyword}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to add URL: {ex.Message}");
        }
    }
}