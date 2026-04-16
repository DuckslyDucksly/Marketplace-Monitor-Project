using System;

namespace OlxMonitor.Core.Models;

// TODO Dodać sprawdzenie czy "Wysyłka OLX"

public class Listing
{
    public int Id { get; set; }                    // EF primary key
    public string OlxId { get; set; } = string.Empty;     // Unique ID from OLX (for duplicate detection)
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? DescriptionSnippet { get; set; }
    public string? ImageUrlsJson { get; set; }            // Store as JSON array for simplicity
    public DateTime PostedDate { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsActive { get; set; } = true;
}