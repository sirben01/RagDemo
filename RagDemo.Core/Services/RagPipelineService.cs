using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using RagDemo.Core.Interfaces;
using RagDemo.Core.Models;

namespace RagDemo.Core.Services;

public class RagPipelineService(
    IWebCrawler crawler,
    ITextChunker chunker,
    IEmbeddingService embeddingService,
    IQdrantService qdrantService,
    IAnthropicService anthropicService,
    ILogger<RagPipelineService> logger) : IRagPipelineService
{
    private const int EmbedBatchSize = 10;

    public async IAsyncEnumerable<IngestProgress> IngestAsync(
        string url,
        string sessionId,
        int maxPages = 50,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new IngestProgress("init", "Ensuring vector collection exists...");
        await qdrantService.EnsureCollectionAsync(cancellationToken);

        yield return new IngestProgress("crawling", $"Starting crawl of {url} (max {maxPages} pages)...");

        var pages = new List<CrawledPage>();
        await foreach (var page in crawler.CrawlAsync(url, maxPages, cancellationToken))
        {
            pages.Add(page);
            yield return new IngestProgress("crawling", $"Crawled: {page.Url}", PagesFound: pages.Count);
        }

        if (pages.Count == 0)
        {
            yield return new IngestProgress("error", "No pages could be crawled.", Error: "No content found at URL.", IsComplete: true);
            yield break;
        }

        yield return new IngestProgress("chunking", $"Chunking {pages.Count} pages...", PagesFound: pages.Count);

        var allChunks = pages
            .SelectMany(p => chunker.Chunk(p.Text, p.Url))
            .ToList();

        logger.LogInformation("Created {ChunkCount} chunks from {PageCount} pages", allChunks.Count, pages.Count);
        yield return new IngestProgress("embedding", $"Embedding {allChunks.Count} chunks...", PagesFound: pages.Count);

        var embeddedChunks = new List<EmbeddedChunk>();
        for (var i = 0; i < allChunks.Count; i += EmbedBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = allChunks.Skip(i).Take(EmbedBatchSize).ToList();
            var vectors = await embeddingService.EmbedBatchAsync(batch.Select(c => c.Text), cancellationToken);

            for (var j = 0; j < batch.Count; j++)
                embeddedChunks.Add(new EmbeddedChunk(batch[j], vectors[j]));

            // Brief pause between batches to stay within OpenAI rate limits
            if (i + EmbedBatchSize < allChunks.Count)
                await Task.Delay(200, cancellationToken);

            yield return new IngestProgress(
                "embedding",
                $"Embedded {Math.Min(i + EmbedBatchSize, allChunks.Count)}/{allChunks.Count} chunks",
                PagesFound: pages.Count,
                ChunksStored: embeddedChunks.Count);
        }

        yield return new IngestProgress("storing", "Storing vectors in Qdrant...", PagesFound: pages.Count, ChunksStored: embeddedChunks.Count);
        await qdrantService.UpsertAsync(embeddedChunks, sessionId, cancellationToken);

        yield return new IngestProgress(
            "complete",
            $"Ingest complete. {pages.Count} pages, {embeddedChunks.Count} chunks stored.",
            PagesFound: pages.Count,
            PagesProcessed: pages.Count,
            ChunksStored: embeddedChunks.Count,
            IsComplete: true);
    }

    public async IAsyncEnumerable<string> ChatAsync(
        string sessionId,
        string question,
        IReadOnlyList<ConversationMessage>? history = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queryVector = await embeddingService.EmbedAsync(question, cancellationToken);
        var results = await qdrantService.SearchAsync(queryVector, sessionId, topK: 5, cancellationToken);

        logger.LogInformation("Retrieved {Count} context chunks for question in session {SessionId}", results.Count, sessionId);

        await foreach (var token in anthropicService.StreamAnswerAsync(question, results, history, cancellationToken))
            yield return token;
    }
}
