using System.Runtime.CompilerServices;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using RagDemo.Core.Interfaces;
using RagDemo.Core.Models;

namespace RagDemo.Core.Services;

public class WebCrawler(IHttpClientFactory httpClientFactory, ILogger<WebCrawler> logger) : IWebCrawler
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
                var html = await FetchHtmlAsync(client, url, cancellationToken);
                if (html is null) continue;

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var text = ExtractText(doc);
                if (!string.IsNullOrWhiteSpace(text))
                    page = new CrawledPage(url, text, DateTime.UtcNow);

                if (depth < MaxDepth)
                {
                    foreach (var link in ExtractLinks(doc, rootUri, url))
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

    private static async Task<string?> FetchHtmlAsync(HttpClient client, string url, CancellationToken ct)
    {
        try
        {
            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                return null;
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractText(HtmlDocument doc)
    {
        // Remove script, style, nav, footer, header nodes
        var nodesToRemove = doc.DocumentNode.SelectNodes(
            "//script|//style|//nav|//footer|//header|//aside|//noscript|//iframe");
        if (nodesToRemove is not null)
            foreach (var node in nodesToRemove.ToList())
                node.Remove();

        var textNodes = doc.DocumentNode.SelectNodes("//body//text()");
        if (textNodes is null) return string.Empty;

        var parts = textNodes
            .Select(n => HtmlEntity.DeEntitize(n.InnerText).Trim())
            .Where(t => t.Length > 0);

        return string.Join(" ", parts);
    }

    private static IEnumerable<string> ExtractLinks(HtmlDocument doc, Uri rootUri, string currentUrl)
    {
        var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchors is null) yield break;

        foreach (var anchor in anchors)
        {
            var href = anchor.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#')) continue;

            if (!Uri.TryCreate(new Uri(currentUrl), href, out var absoluteUri)) continue;
            if (absoluteUri.Host != rootUri.Host) continue;
            if (absoluteUri.Scheme is not "http" and not "https") continue;

            // Strip fragment and query for deduplication
            var clean = new UriBuilder(absoluteUri) { Fragment = "", Query = "" }.Uri.ToString().TrimEnd('/');
            yield return clean;
        }
    }
}
