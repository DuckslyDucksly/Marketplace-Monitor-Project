using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using OlxMonitor.Core.Models;
using System.Text.Json;

namespace OlxMonitor.Infrastructure.Services;

public class OlxScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OlxScraper> _logger;
    private readonly Random _random = new();

    public OlxScraper(HttpClient httpClient, ILogger<OlxScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Polite headers - important because OLX fights bots hard
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(GetRandomUserAgent());
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pl-PL,pl;q=0.9");
    }

    public async Task<List<Listing>> ScrapeAsync(string categoryPath, string keyword, int maxPages = 2)
    {
        var listings = new List<Listing>();
        var baseUrl = $"https://www.olx.pl{categoryPath}q-{keyword.Replace(" ", "-")}/";

        _logger.LogInformation("Scraping OLX: {Url}", baseUrl);

        for (int page = 1; page <= maxPages; page++)
        {
            var url = page == 1 ? baseUrl : $"{baseUrl}?page={page}";

            try
            {
                var html = await _httpClient.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var adNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'css-1sw7q1x')]"); // OLX ad container class (may need update)

                if (adNodes == null) break;

                foreach (var node in adNodes)
                {
                    try
                    {
                        var listing = ParseListing(node);
                        if (listing != null)
                            listings.Add(listing);
                    }
                    catch { /* skip broken ads */ }
                }

                // Be polite - random delay between pages
                await Task.Delay(_random.Next(4000, 8000));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scrape page {Page}", page);
                break;
            }
        }

        return listings;
    }

    private Listing? ParseListing(HtmlNode node)
    {
        // This selector will likely need small tuning — OLX changes class names often
        var titleNode = node.SelectSingleNode(".//h6") ?? node.SelectSingleNode(".//a");
        var priceNode = node.SelectSingleNode(".//p[contains(@class, 'css-1q1v8m5')]");
        var locationNode = node.SelectSingleNode(".//p[contains(@class, 'css-1e3p1d')]");
        var linkNode = node.SelectSingleNode(".//a");

        if (titleNode == null || linkNode == null) return null;

        var url = linkNode.GetAttributeValue("href", "");
        if (!url.StartsWith("http")) url = "https://www.olx.pl" + url;

        var olxId = url.Split('/').LastOrDefault()?.Split('-').LastOrDefault() ?? "";

        return new Listing
        {
            OlxId = olxId,
            Title = titleNode.InnerText.Trim(),
            Price = ParsePrice(priceNode?.InnerText),
            Location = locationNode?.InnerText.Trim() ?? "",
            Url = url,
            PostedDate = DateTime.UtcNow,           // OLX doesn't always show exact date
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow
        };
    }

    private decimal ParsePrice(string? priceText)
    {
        if (string.IsNullOrEmpty(priceText)) return 0;
        var clean = priceText.Replace("zł", "").Replace(" ", "").Replace(",", ".");
        return decimal.TryParse(clean, out var price) ? price : 0;
    }

    private string GetRandomUserAgent()
    {
        var agents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36"
        };
        return agents[_random.Next(agents.Length)];
    }
}