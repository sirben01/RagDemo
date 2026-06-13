using RagDemo.Core.Models;

namespace RagDemo.Core.Interfaces;

public interface IAnthropicService
{
    IAsyncEnumerable<string> StreamAnswerAsync(
        string question,
        IReadOnlyList<SearchResult> context,
        IReadOnlyList<ConversationMessage>? history = null,
        CancellationToken cancellationToken = default);
}
