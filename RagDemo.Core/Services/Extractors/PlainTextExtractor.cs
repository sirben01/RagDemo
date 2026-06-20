using System.Text.RegularExpressions;
using RagDemo.Core.Interfaces;

namespace RagDemo.Core.Services.Extractors;

public class PlainTextExtractor : IContentExtractor
{
    private static readonly HashSet<string> PlainExtensions =
        new([".txt", ".md", ".markdown", ".rst", ".csv"], StringComparer.OrdinalIgnoreCase);

    public bool CanHandle(string contentType, string url)
    {
        if (contentType is "text/plain" or "text/markdown") return true;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var ext = Path.GetExtension(uri.AbsolutePath);
            return PlainExtensions.Contains(ext);
        }
        return PlainExtensions.Contains(Path.GetExtension(url));
    }

    private static readonly Regex MarkdownSyntax = new(@"[#*`_~\[\]()>]", RegexOptions.Compiled);

    public async Task<string> ExtractTextAsync(Stream content, string url, CancellationToken ct = default)
    {
        using var reader = new StreamReader(content);
        var raw = await reader.ReadToEndAsync(ct);

        // Strip Markdown syntax characters for cleaner embedding
        raw = MarkdownSyntax.Replace(raw, " ");
        return string.Join(" ", raw.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries));
    }
}
