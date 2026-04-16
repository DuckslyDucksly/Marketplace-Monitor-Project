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

namespace OlxMonitor.Worker;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        // Handle "add-url" command
        if (args.Length == 2 && args[0].Equals("add-url", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var searchItem = SearchItem.FromUrl(args[1]);
                AddToConfig(searchItem);
                Log.Information("✅ Added new search: Category={Category} Keyword={Keyword}", 
                    searchItem.CategoryPath, searchItem.Keyword);
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to add URL");
                return;
            }
        }

        // Normal worker mode
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

        // Apply migrations
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OlxMonitorDbContext>();
            await db.Database.MigrateAsync();
            Log.Information("✅ Database ready");
        }

        Log.Information("🚀 OLX Monitor starting...");
        await host.RunAsync();
    }

    private static void AddToConfig(SearchItem newItem)
{
    var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

    // Load existing config or create new
    string json = File.Exists(configPath) ? File.ReadAllText(configPath) : "{}";
    var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();

    // Ensure MonitorSettings section exists
    if (!config.ContainsKey("MonitorSettings") || config["MonitorSettings"] is not Dictionary<string, object> monitorSettings)
    {
        monitorSettings = new Dictionary<string, object>
        {
            ["CheckIntervalMinutes"] = 5,
            ["MaxPages"] = 2,
            ["Searches"] = new List<object>()
        };
        config["MonitorSettings"] = monitorSettings;
    }

    // Ensure Searches list exists
    if (!monitorSettings.ContainsKey("Searches") || monitorSettings["Searches"] is not List<object> searches)
    {
        searches = new List<object>();
        monitorSettings["Searches"] = searches;
    }

    // Check for duplicate keyword
    if (searches.Any(s => 
        s is IDictionary<string, object> dict && 
        dict.TryGetValue("Keyword", out var keywordObj) && 
        keywordObj?.ToString() == newItem.Keyword))
    {
        Log.Information("Search with keyword '{Keyword}' already exists", newItem.Keyword);
        return;
    }

    // Add the new search
    searches.Add(new Dictionary<string, object>
    {
        ["CategoryPath"] = newItem.CategoryPath,
        ["Keyword"] = newItem.Keyword
    });

    // Save back to file
    File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));

    Log.Information("✅ Successfully added new search → Category: {Category} | Keyword: {Keyword}", 
        newItem.CategoryPath, newItem.Keyword);
}
}