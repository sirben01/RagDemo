using RagDemo.Core.Models;

namespace RagDemo.Core.Interfaces;

public interface IRagPipelineService
{
    IAsyncEnumerable<IngestProgress> IngestAsync(string url, string sessionId, int maxPages = 50, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> ChatAsync(string sessionId, string question, IReadOnlyList<ConversationMessage>? history = null, CancellationToken cancellationToken = default);
}
