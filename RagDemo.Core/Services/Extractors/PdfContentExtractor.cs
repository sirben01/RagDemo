using System.Text;
using RagDemo.Core.Interfaces;
using UglyToad.PdfPig;

namespace RagDemo.Core.Services.Extractors;

public class PdfContentExtractor : IContentExtractor
{
    public bool CanHandle(string contentType, string url) =>
        contentType == "application/pdf" ||
        url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<string> ExtractTextAsync(Stream content, string url, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        using var pdf = PdfDocument.Open(ms);
        var sb = new StringBuilder();

        foreach (var page in pdf.GetPages())
        {
            var pageText = string.Join(" ", page.GetWords().Select(w => w.Text));
            sb.AppendLine(pageText);
        }

        return sb.ToString();
    }
}
