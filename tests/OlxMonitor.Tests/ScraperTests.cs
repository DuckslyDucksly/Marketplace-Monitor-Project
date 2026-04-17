using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Moq;
using OlxMonitor.Infrastructure.Services;
using Xunit;
using FluentAssertions;

namespace OlxMonitor.Tests;

public class ScraperTests
{
    private readonly OlxScraper _scraper;

    public ScraperTests()
    {
        var httpClient = new HttpClient();
        var loggerMock = new Mock<ILogger<OlxScraper>>();

        _scraper = new OlxScraper(httpClient, loggerMock.Object);
    }

    [Fact]
    public void ParseListing_ShouldExtractCorrectData_FromValidNode()
    {
        var html = """
            <div data-cy="l-card">
                <h6>DDR4 16 GB GoodRam IRDM x Black</h6>
                <p>450 zł</p>
                <p data-testid="location">Czechowice-Dziedzice</p>
                <a href="/oferta/ddr4-16-gb-goodram-irmd-ID987654.html"></a>
            </div>
        """;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var node = doc.DocumentNode.SelectSingleNode("//div");

        var listing = _scraper.ParseListing(node);

        listing.Should().NotBeNull();
        listing!.Title.Should().Contain("GoodRam IRDM");
        listing.Price.Should().Be(450);
        listing.Location.Should().Be("Czechowice-Dziedzice");   // this should now pass
        listing.Url.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("450 zł", 450)]
    [InlineData("2 649 zł", 2649)]
    [InlineData("2649zł", 2649)]
    [InlineData("2,649 zł", 2649)]
    [InlineData("Nie podano", 0)]
    [InlineData(null, 0)]
    public void ParsePrice_ShouldHandleVariousFormats(string? input, decimal expected)
    {
        var result = _scraper.ParsePrice(input);
        result.Should().Be(expected);
    }
}