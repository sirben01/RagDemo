using HtmlAgilityPack;
using RagDemo.Core.Interfaces;

namespace RagDemo.Core.Services.Extractors;

public class HtmlContentExtractor : IContentExtractor
{
    public bool CanHandle(string contentType, string url) =>
        contentType is "text/html" or "application/xhtml+xml";

    public Task<string> ExtractTextAsync(Stream content, string url, CancellationToken ct = default)
    {
        var (text, _) = ParseDocument(content, url, rootUri: null);
        return Task.FromResult(text);
    }

    // Single-parse path used by WebCrawler to avoid building the DOM twice.
    public (string Text, List<string> Links) ExtractTextAndLinks(byte[] bytes, Uri rootUri, string currentUrl)
    {
        using var ms = new MemoryStream(bytes);
        return ParseDocument(ms, currentUrl, rootUri);
    }

    private static (string Text, List<string> Links) ParseDocument(Stream content, string currentUrl, Uri? rootUri)
    {
        var doc = new HtmlDocument();
        doc.Load(content);

        // Extract links before removing nav/header/footer — those elements contain navigation links.
        var links = new List<string>();
        if (rootUri is not null)
        {
            var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchors is not null)
            {
                foreach (var anchor in anchors)
                {
                    var href = anchor.GetAttributeValue("href", "");
                    if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#')) continue;
                    if (!Uri.TryCreate(new Uri(currentUrl), href, out var absoluteUri)) continue;
                    if (absoluteUri.Host != rootUri.Host) continue;
                    if (absoluteUri.Scheme is not "http" and not "https") continue;
                    var clean = new UriBuilder(absoluteUri) { Fragment = "", Query = "" }.Uri.ToString().TrimEnd('/');
                    links.Add(clean);
                }
            }
        }

        var noiseNodes = doc.DocumentNode
            .SelectNodes("//script|//style|//nav|//footer|//header|//aside|//noscript|//iframe");
        if (noiseNodes is not null)
            foreach (var node in noiseNodes.ToList())
                node.Remove();

        var textNodes = doc.DocumentNode.SelectNodes("//body//text()");
        var text = textNodes is null
            ? string.Empty
            : string.Join(" ", textNodes
                .Select(n => HtmlEntity.DeEntitize(n.InnerText).Trim())
                .Where(t => t.Length > 0));

        return (text, links);
    }
}
