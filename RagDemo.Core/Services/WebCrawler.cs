using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using RagDemo.Core.Interfaces;
using RagDemo.Core.Models;
using RagDemo.Core.Services.Extractors;

namespace RagDemo.Core.Services;

public class WebCrawler(
    IHttpClientFactory httpClientFactory,
    ContentExtractorFactory extractorFactory,
    ILogger<WebCrawler> logger) : IWebCrawler
{
    private const int MaxDepth = 3;

    public async IAsyncEnumerable<CrawledPage> CrawlAsync(
        string rootUrl,
        int maxPages = 50,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rootUri = new Uri(rootUrl);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Url, int Depth)>();
        queue.Enqueue((rootUrl, 0));

        var client = httpClientFactory.CreateClient("crawler");
        var successCount = 0;

        // Guard on pages-with-content, not attempted URLs; visited is unbounded for dedup only
        while (queue.Count > 0 && successCount < maxPages && !cancellationToken.IsCancellationRequested)
        {
            var (url, depth) = queue.Dequeue();

            if (!visited.Add(url)) continue;

            CrawledPage? page = null;
            try
            {
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogDebug("Non-success status {Status} for {Url}", response.StatusCode, url);
                    continue;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/html";
                var extractor = extractorFactory.GetExtractor(contentType, url);

                if (extractor is null)
                {
                    logger.LogDebug("No extractor for content type {ContentType} at {Url} — skipping", contentType, url);
                    continue;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                if (extractor is HtmlContentExtractor htmlExtractor)
                {
                    // Single DOM parse yields both text and outbound links
                    var (text, links) = htmlExtractor.ExtractTextAndLinks(bytes, rootUri, url);

                    if (!string.IsNullOrWhiteSpace(text))
                        page = new CrawledPage(url, text, DateTime.UtcNow);

                    if (depth < MaxDepth)
                    {
                        foreach (var link in links)
                            if (!visited.Contains(link))
                                queue.Enqueue((link, depth + 1));
                    }
                }
                else
                {
                    using var extractStream = new MemoryStream(bytes);
                    var text = await extractor.ExtractTextAsync(extractStream, url, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(text))
                        page = new CrawledPage(url, text, DateTime.UtcNow);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to crawl {Url}", url);
            }

            if (page is not null)
            {
                successCount++;
                yield return page;
            }
        }
    }
}
