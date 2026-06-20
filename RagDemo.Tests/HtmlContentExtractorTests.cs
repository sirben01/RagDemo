using System.Text;
using RagDemo.Core.Services.Extractors;

namespace RagDemo.Tests;

public class HtmlContentExtractorTests
{
    private readonly HtmlContentExtractor _extractor = new();
    private static readonly Uri RootUri = new("https://example.com");
    private const string CurrentUrl = "https://example.com/page";

    // ── CanHandle ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/xhtml+xml")]
    public void CanHandle_ReturnsTrueForHtmlTypes(string contentType)
    {
        Assert.True(_extractor.CanHandle(contentType, "https://example.com"));
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("image/png")]
    public void CanHandle_ReturnsFalseForNonHtmlTypes(string contentType)
    {
        Assert.False(_extractor.CanHandle(contentType, "https://example.com"));
    }

    // ── Link extraction ──────────────────────────────────────────────────────

    [Fact]
    public void ExtractTextAndLinks_FindsLinksInNav()
    {
        var html = """
            <html><body>
              <nav><a href="/about">About</a><a href="/contact">Contact</a></nav>
              <main><p>Main content here.</p></main>
            </body></html>
            """;

        var (_, links) = Extract(html);

        Assert.Contains("https://example.com/about", links);
        Assert.Contains("https://example.com/contact", links);
    }

    [Fact]
    public void ExtractTextAndLinks_FindsLinksInHeader()
    {
        var html = """
            <html><body>
              <header><a href="/home">Home</a></header>
              <p>Body text.</p>
            </body></html>
            """;

        var (_, links) = Extract(html);

        Assert.Contains("https://example.com/home", links);
    }

    [Fact]
    public void ExtractTextAndLinks_ExcludesExternalLinks()
    {
        var html = """
            <html><body>
              <a href="https://other.com/page">External</a>
              <a href="/internal">Internal</a>
            </body></html>
            """;

        var (_, links) = Extract(html);

        Assert.DoesNotContain("https://other.com/page", links);
        Assert.Contains("https://example.com/internal", links);
    }

    [Fact]
    public void ExtractTextAndLinks_ExcludesFragmentOnlyLinks()
    {
        var html = """
            <html><body>
              <a href="#section">Jump</a>
              <a href="/real">Real</a>
            </body></html>
            """;

        var (_, links) = Extract(html);

        Assert.DoesNotContain(links, l => l.Contains('#'));
        Assert.Contains("https://example.com/real", links);
    }

    [Fact]
    public void ExtractTextAndLinks_StripsFragmentsAndQueryStrings()
    {
        var html = """
            <html><body>
              <a href="/page?ref=menu#top">Link</a>
            </body></html>
            """;

        var (_, links) = Extract(html);

        Assert.Contains("https://example.com/page", links);
        Assert.DoesNotContain(links, l => l.Contains('?') || l.Contains('#'));
    }

    [Fact]
    public void ExtractTextAndLinks_ResolvesRelativeLinks()
    {
        var html = """
            <html><body>
              <a href="subpage">Relative</a>
            </body></html>
            """;

        var (_, links) = Extract(html);

        Assert.Contains("https://example.com/subpage", links);
    }

    // ── Text extraction ──────────────────────────────────────────────────────

    [Fact]
    public void ExtractTextAndLinks_ReturnsBodyText()
    {
        var html = """
            <html><body>
              <p>Hello world from the body.</p>
            </body></html>
            """;

        var (text, _) = Extract(html);

        Assert.Contains("Hello world from the body", text);
    }

    [Fact]
    public void ExtractTextAndLinks_ExcludesNavTextFromExtractedText()
    {
        var html = """
            <html><body>
              <nav>Site navigation menu</nav>
              <main><p>Actual content paragraph.</p></main>
            </body></html>
            """;

        var (text, _) = Extract(html);

        Assert.DoesNotContain("Site navigation menu", text);
        Assert.Contains("Actual content paragraph", text);
    }

    [Fact]
    public void ExtractTextAndLinks_ExcludesScriptContent()
    {
        var html = """
            <html><body>
              <script>var x = 'injected script';</script>
              <p>Real paragraph.</p>
            </body></html>
            """;

        var (text, _) = Extract(html);

        Assert.DoesNotContain("injected script", text);
        Assert.Contains("Real paragraph", text);
    }

    [Fact]
    public void ExtractTextAndLinks_EmptyBody_ReturnsEmptyText()
    {
        var html = "<html><body></body></html>";
        var (text, _) = Extract(html);
        Assert.True(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void ExtractTextAndLinks_NavLinksDiscoveredButNavTextExcluded()
    {
        // The key regression test: links in nav are found AND nav text is stripped.
        var html = """
            <html><body>
              <nav><a href="/services">Services</a></nav>
              <p>Welcome to our site.</p>
            </body></html>
            """;

        var (text, links) = Extract(html);

        Assert.Contains("https://example.com/services", links);
        Assert.DoesNotContain("Services", text);
        Assert.Contains("Welcome to our site", text);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private (string Text, List<string> Links) Extract(string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        return _extractor.ExtractTextAndLinks(bytes, RootUri, CurrentUrl);
    }
}
