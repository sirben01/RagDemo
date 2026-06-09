using RagDemo.Core.Models;

namespace RagDemo.Core.Interfaces;

public interface IQdrantService
{
    Task EnsureCollectionAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(IEnumerable<EmbeddedChunk> chunks, string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResult>> SearchAsync(float[] queryVector, string sessionId, int topK = 5, CancellationToken cancellationToken = default);
    Task<SessionStats> GetSessionStatsAsync(string sessionId, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
