using System.Text.RegularExpressions;

namespace OlxMonitor.Core.Models;

public class MonitorSettings
{
    public int CheckIntervalMinutes { get; set; } = 5;
    public int MaxPages { get; set; } = 2;
    public List<SearchItem> Searches { get; set; } = new();
}

public class SearchItem
{
    public string CategoryPath { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;

    // Helper to create from full OLX URL
    public static SearchItem FromUrl(string fullUrl)
    {
        if (string.IsNullOrWhiteSpace(fullUrl))
            throw new ArgumentException("URL cannot be empty");

        var uri = new Uri(fullUrl.TrimEnd('/'));
        var path = uri.AbsolutePath;

        // Extract category path and keyword (q-xxx)
        var match = Regex.Match(path, @"(/elektronika/.+?)/q-([^/]+)", RegexOptions.IgnoreCase);
        
        if (!match.Success)
            throw new ArgumentException("Invalid OLX search URL. Example: https://www.olx.pl/elektronika/komputery/q-ddr5-ram/");

        return new SearchItem
        {
            CategoryPath = match.Groups[1].Value + "/",
            Keyword = match.Groups[2].Value.Replace("-", " ")
        };
    }
}