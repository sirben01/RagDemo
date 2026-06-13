using System.Xml.Linq;
using RagDemo.Core.Interfaces;

namespace RagDemo.Core.Services.Extractors;

public class XmlContentExtractor : IContentExtractor
{
    public bool CanHandle(string contentType, string url) =>
        contentType is "application/xml" or "text/xml" or "application/rss+xml" or "application/atom+xml"
        || url.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    public Task<string> ExtractTextAsync(Stream content, string url, CancellationToken ct = default)
    {
        var doc = XDocument.Load(content);
        var text = string.Join(" ", doc.DescendantNodes()
            .OfType<XText>()
            .Select(n => n.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v)));

        return Task.FromResult(text);
    }
}
