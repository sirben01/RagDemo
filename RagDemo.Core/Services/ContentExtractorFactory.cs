using RagDemo.Core.Interfaces;

namespace RagDemo.Core.Services;

public class ContentExtractorFactory(IEnumerable<IContentExtractor> extractors)
{
    public IContentExtractor? GetExtractor(string contentType, string url)
    {
        var normalized = contentType.Split(';')[0].Trim().ToLowerInvariant();
        return extractors.FirstOrDefault(e => e.CanHandle(normalized, url));
    }
}
