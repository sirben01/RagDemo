using RagDemo.Core.Models;

namespace RagDemo.Core.Interfaces;

public interface ITextChunker
{
    IEnumerable<TextChunk> Chunk(string text, string sourceUrl);
}
