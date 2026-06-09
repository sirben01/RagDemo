using RagDemo.Core.Models;

namespace RagDemo.Core.Interfaces;

public interface IWebCrawler
{
    IAsyncEnumerable<CrawledPage> CrawlAsync(string rootUrl, int maxPages = 50, CancellationToken cancellationToken = default);
}
