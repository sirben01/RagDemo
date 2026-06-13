using HtmlAgilityPack;
using RagDemo.Core.Interfaces;

namespace RagDemo.Core.Services.Extractors;

public class HtmlContentExtractor : IContentExtractor
{
    public bool CanHandle(string contentType, string url) =>
        contentType is "text/html" or "application/xhtml+xml";

    public Task<string> ExtractTextAsync(Stream content, string url, CancellationToken ct = default)
    {
        var doc = new HtmlDocument();
        doc.Load(content);

        var noiseNodes = doc.DocumentNode
            .SelectNodes("//script|//style|//nav|//footer|//header|//aside|//noscript|//iframe");
        if (noiseNodes is not null)
            foreach (var node in noiseNodes.ToList())
                node.Remove();

        var textNodes = doc.DocumentNode.SelectNodes("//body//text()");
        if (textNodes is null) return Task.FromResult(string.Empty);

        var parts = textNodes
            .Select(n => HtmlEntity.DeEntitize(n.InnerText).Trim())
            .Where(t => t.Length > 0);

        return Task.FromResult(string.Join(" ", parts));
    }
}
