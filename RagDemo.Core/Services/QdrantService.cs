using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RagDemo.Core.Interfaces;
using RagDemo.Core.Models;

namespace RagDemo.Core.Services;

public class QdrantOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6334;
    public string CollectionName { get; set; } = "rag_chunks";
    public int VectorSize { get; set; } = 1536; // text-embedding-3-small dimension
}

public class QdrantService(IOptions<QdrantOptions> options, ILogger<QdrantService> logger) : IQdrantService
{
    private readonly QdrantClient _client = new(options.Value.Host, options.Value.Port);
    private readonly string _collection = options.Value.CollectionName;
    private readonly int _vectorSize = options.Value.VectorSize;

    public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
    {
        var collections = await _client.ListCollectionsAsync(cancellationToken);
        if (collections.Any(c => c == _collection)) return;

        logger.LogInformation("Creating Qdrant collection {Collection}", _collection);
        await _client.CreateCollectionAsync(
            _collection,
            new VectorParams { Size = (ulong)_vectorSize, Distance = Distance.Cosine },
            cancellationToken: cancellationToken);
    }

    public async Task UpsertAsync(IEnumerable<EmbeddedChunk> chunks, string sessionId, CancellationToken cancellationToken = default)
    {
        var points = chunks.Select(chunk =>
        {
            var point = new PointStruct
            {
                Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                Vectors = chunk.Vector,
                Payload =
                {
                    ["text"] = chunk.Chunk.Text,
                    ["source_url"] = chunk.Chunk.SourceUrl,
                    ["session_id"] = sessionId,
                    ["chunk_index"] = chunk.Chunk.ChunkIndex
                }
            };
            return point;
        }).ToList();

        await _client.UpsertAsync(_collection, points, cancellationToken: cancellationToken);
        logger.LogDebug("Upserted {Count} points for session {SessionId}", points.Count, sessionId);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryVector,
        string sessionId,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var filter = SessionFilter(sessionId);

        var results = await _client.SearchAsync(
            _collection,
            queryVector,
            filter: filter,
            limit: (ulong)topK,
            cancellationToken: cancellationToken);

        return results.Select(r => new SearchResult(
            r.Payload["text"].StringValue,
            r.Payload["source_url"].StringValue,
            r.Score)).ToList();
    }

    public async Task<SessionStats> GetSessionStatsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var collections = await _client.ListCollectionsAsync(cancellationToken);
        if (!collections.Any(c => c == _collection))
            return new SessionStats(sessionId, 0, 0, [], null);

        var filter = SessionFilter(sessionId);

        // Count total chunks for this session
        var count = await _client.CountAsync(_collection, filter: filter, exact: true, cancellationToken: cancellationToken);

        // Scroll to collect unique source URLs (limited sample)
        var urls = new HashSet<string>();
        var scrollResponse = await _client.ScrollAsync(
            _collection,
            filter,
            500,
            cancellationToken: cancellationToken);

        foreach (var p in scrollResponse.Result)
        {
            if (p.Payload.TryGetValue("source_url", out var urlVal))
                urls.Add(urlVal.StringValue);
        }

        return new SessionStats(
            sessionId,
            urls.Count,
            (int)count,
            [.. urls],
            DateTime.UtcNow);
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var filter = SessionFilter(sessionId);
        await _client.DeleteAsync(_collection, filter, cancellationToken: cancellationToken);
        logger.LogInformation("Deleted all points for session {SessionId}", sessionId);
    }

    private static Filter SessionFilter(string sessionId) => new()
    {
        Must =
        {
            new Condition
            {
                Field = new FieldCondition
                {
                    Key = "session_id",
                    Match = new Match { Keyword = sessionId }
                }
            }
        }
    };
}
