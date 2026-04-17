using System.Runtime.CompilerServices;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.RegularExpressions;
using OlxMonitor.Core.Models;

[assembly: InternalsVisibleTo("OlxMonitor.Tests")]

namespace OlxMonitor.Infrastructure.Services;

public class OlxScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OlxScraper> _logger;
    private readonly Random _random = new();

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
        var safeKeyword = keyword.Replace(" ", "-").ToLowerInvariant();
        var baseUrl = $"https://www.olx.pl{categoryPath}q-{safeKeyword}/";

        _logger.LogInformation("Scraping: {Url}", baseUrl);

        for (int page = 1; page <= maxPages; page++)
        {
            var url = page == 1 ? baseUrl : $"{baseUrl}?page={page}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var adNodes = doc.DocumentNode.SelectNodes("//div[contains(@data-cy, 'l-card')]") 
                             ?? doc.DocumentNode.SelectNodes("//article[contains(@class, 'css-')]");

                if (adNodes == null || adNodes.Count == 0)
                {
                    _logger.LogWarning("No listing cards found on page {Page}. OLX may have changed structure.", page);
                    break;
                }

                foreach (var node in adNodes)
                {
                    var listing = ParseListing(node);
                    if (listing != null)
                        listings.Add(listing);
                }

                await Task.Delay(_random.Next(6000, 14000)); // polite delay
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load page {Page}", page);
                break;
            }
        }

        _logger.LogInformation("Scraped {Count} potential listings from {Pages} pages", listings.Count, maxPages);
        return listings;
    }

    internal Listing? ParseListing(HtmlNode node)
    {
        try
        {
            var titleNode = node.SelectSingleNode(".//h6") 
                          ?? node.SelectSingleNode(".//h4") 
                          ?? node.SelectSingleNode(".//a[contains(@class, 'css-') and not(contains(@class, 'price'))]") 
                          ?? node.SelectSingleNode(".//div[contains(@class, 'title')]//h6")
                          ?? node.SelectSingleNode(".//span[string-length(text()) > 15]");

            var priceNode = node.SelectSingleNode(".//p[contains(text(),'zł')]") 
                          ?? node.SelectSingleNode(".//span[contains(text(),'zł')]") 
                          ?? node.SelectSingleNode(".//div[contains(text(),'zł')]");

            var locationNode = node.SelectSingleNode(".//p[contains(@data-testid,'location')]") 
                            ?? node.SelectSingleNode(".//span[contains(@class,'location')]");

            var linkNode = node.SelectSingleNode(".//a[contains(@href,'/oferta/')]");

            if (linkNode == null) return null;

            var relativeUrl = linkNode.GetAttributeValue("href", "");
            var fullUrl = relativeUrl.StartsWith("http") ? relativeUrl : "https://www.olx.pl" + relativeUrl;

            var olxId = fullUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";

            var rawTitle = titleNode?.InnerText?.Trim() ?? "";

            var title = rawTitle
                .Replace("Dostawa gratis", "")
                .Replace("Odświeżono", "")
                .Trim();

            return new Listing
            {
                OlxId = olxId,
                Title = string.IsNullOrWhiteSpace(title) ? "Untitled Listing" : title,
                Price = ParsePrice(priceNode?.InnerText),
                Location = locationNode?.InnerText?.Trim() ?? "Nieznana lokalizacja",
                Url = fullUrl,
                PostedDate = DateTime.UtcNow,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }

    internal decimal ParsePrice(string? priceText)
    {
    if (string.IsNullOrWhiteSpace(priceText)) 
        return 0m;

    var clean = priceText
        .Replace("zł", "")
        .Replace(" ", "")
        .Replace(",", ".")           // Polish comma to dot
        .Replace("do negocjacji", "")
        .Replace("netto", "")
        .Replace("brutto", "")
        .Replace("Za darmo", "0")
        .Trim();

    clean = Regex.Replace(clean, @"[^\d.]", "");

    // If it looks like "2.649" (thousands separator), remove the dot
    if (clean.Contains('.') && clean.Split('.').Length == 2)
    {
        var parts = clean.Split('.');
        if (parts[1].Length == 3)
            clean = parts[0] + parts[1];   // becomes "2649"
    }

    return decimal.TryParse(clean, System.Globalization.NumberStyles.Any, 
        System.Globalization.CultureInfo.InvariantCulture, out var price) 
        ? price 
        : 0m;
    }
}