using RagDemo.Core.Interfaces;
using RagDemo.Core.Models;

namespace RagDemo.Core.Services;

public class TextChunker : ITextChunker
{
    // Approximate: 1 token ≈ 4 chars for English text
    private const int TargetTokens = 600;
    private const int OverlapTokens = 100;
    private const int CharsPerToken = 4;

    private static readonly int ChunkSize = TargetTokens * CharsPerToken;
    private static readonly int OverlapSize = OverlapTokens * CharsPerToken;

    public IEnumerable<TextChunk> Chunk(string text, string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        // Normalize whitespace
        text = string.Join(" ", text.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries));

        var index = 0;
        var chunkIndex = 0;

        while (index < text.Length)
        {
            var end = Math.Min(index + ChunkSize, text.Length);

            // Extend to next sentence boundary if not at end
            if (end < text.Length)
            {
                var sentenceEnd = FindSentenceBoundary(text, end);
                if (sentenceEnd > index) end = sentenceEnd;
            }

            var chunkText = text[index..end].Trim();
            if (!string.IsNullOrWhiteSpace(chunkText))
                yield return new TextChunk(chunkText, sourceUrl, chunkIndex++);

            // Step forward by chunk size minus overlap
            index += ChunkSize - OverlapSize;
        }
    }

    private static int FindSentenceBoundary(string text, int nearIndex)
    {
        // Look forward up to 200 chars for a sentence-ending punctuation followed by whitespace
        var limit = Math.Min(nearIndex + 200, text.Length);
        for (var i = nearIndex; i < limit; i++)
        {
            if (text[i] is '.' or '!' or '?' && (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1])))
                return i + 1;
        }
        // Fall back: nearest whitespace
        for (var i = nearIndex; i > nearIndex - 100 && i >= 0; i--)
        {
            if (char.IsWhiteSpace(text[i]))
                return i;
        }
        return nearIndex;
    }
}
