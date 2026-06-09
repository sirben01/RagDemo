using RagDemo.Core.Models;

namespace RagDemo.Core.Interfaces;

public interface IAnthropicService
{
    IAsyncEnumerable<string> StreamAnswerAsync(
        string question,
        IReadOnlyList<SearchResult> context,
        CancellationToken cancellationToken = default);
}
