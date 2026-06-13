using System.Runtime.CompilerServices;
using HtmlAgilityPack;
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

        while (queue.Count > 0 && visited.Count < maxPages && !cancellationToken.IsCancellationRequested)
        {
            var (url, depth) = queue.Dequeue();

            if (!visited.Add(url)) continue;

            CrawledPage? page = null;
            try
            {
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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

                // Buffer content so we can use it for both extraction and link-following
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                using var extractStream = new MemoryStream(bytes);
                var text = await extractor.ExtractTextAsync(extractStream, url, cancellationToken);

                if (!string.IsNullOrWhiteSpace(text))
                    page = new CrawledPage(url, text, DateTime.UtcNow);

                // Only follow links from HTML pages
                if (extractor is HtmlContentExtractor && depth < MaxDepth)
                {
                    using var linkStream = new MemoryStream(bytes);
                    foreach (var link in ExtractLinks(linkStream, rootUri, url))
                    {
                        if (!visited.Contains(link))
                            queue.Enqueue((link, depth + 1));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to crawl {Url}", url);
            }

            if (page is not null)
                yield return page;
        }
    }

    private static IEnumerable<string> ExtractLinks(Stream html, Uri rootUri, string currentUrl)
    {
        var doc = new HtmlDocument();
        doc.Load(html);

        var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchors is null) yield break;

        foreach (var anchor in anchors)
        {
            var href = anchor.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#')) continue;

            if (!Uri.TryCreate(new Uri(currentUrl), href, out var absoluteUri)) continue;
            if (absoluteUri.Host != rootUri.Host) continue;
            if (absoluteUri.Scheme is not "http" and not "https") continue;

            var clean = new UriBuilder(absoluteUri) { Fragment = "", Query = "" }.Uri.ToString().TrimEnd('/');
            yield return clean;
        }
    }
}
