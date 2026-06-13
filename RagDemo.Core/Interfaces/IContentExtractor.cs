namespace RagDemo.Core.Interfaces;

public interface IContentExtractor
{
    bool CanHandle(string contentType, string url);
    Task<string> ExtractTextAsync(Stream content, string url, CancellationToken ct = default);
}
