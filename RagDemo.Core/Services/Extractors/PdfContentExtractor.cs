using System.Text;
using RagDemo.Core.Interfaces;
using UglyToad.PdfPig;

namespace RagDemo.Core.Services.Extractors;

public class PdfContentExtractor : IContentExtractor
{
    public bool CanHandle(string contentType, string url) =>
        contentType == "application/pdf" ||
        url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<string> ExtractTextAsync(Stream content, string url, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);

        using var pdf = PdfDocument.Open(ms.ToArray());
        var sb = new StringBuilder();

        foreach (var page in pdf.GetPages())
        {
            var pageText = string.Join(" ", page.GetWords().Select(w => w.Text));
            sb.AppendLine(pageText);
        }

        return Task.FromResult(sb.ToString());
    }
}
