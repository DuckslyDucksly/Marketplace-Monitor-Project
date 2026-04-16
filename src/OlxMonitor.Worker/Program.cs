using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OlxMonitor.Infrastructure.Data;
using Serilog;

namespace OlxMonitor.Worker;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        IHost host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((hostContext, services) =>
            {
                // SQLite with persistent file (Docker volume will mount here)
                services.AddDbContext<OlxMonitorDbContext>(options =>
                    options.UseSqlite("Data Source=/app/data/olxmonitor.db"));

                // TODO: Add scraper and background service here later
            })
            .Build();

        // Ensure DB and migrations on startup
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OlxMonitorDbContext>();
            db.Database.Migrate();
        }

        host.Run();
    }
}