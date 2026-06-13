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

    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "mr", "mrs", "ms", "dr", "prof", "sr", "jr", "vs", "etc", "eg", "ie", "fig", "approx"
    };

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

            // Step from actual end to preserve consistent overlap
            index = end - OverlapSize;
        }
    }

    private static int FindSentenceBoundary(string text, int nearIndex)
    {
        var limit = Math.Min(nearIndex + 200, text.Length);
        for (var i = nearIndex; i < limit; i++)
        {
            if (text[i] is not ('.' or '!' or '?')) continue;
            if (i + 1 >= text.Length || !char.IsWhiteSpace(text[i + 1])) continue;

            // Guard abbreviations — get the word preceding the punctuation
            var wordStart = i - 1;
            while (wordStart > 0 && char.IsLetter(text[wordStart - 1])) wordStart--;
            var word = text[wordStart..i];
            if (Abbreviations.Contains(word)) continue;

            // Next non-whitespace should be uppercase or a digit
            var nextChar = i + 2 < text.Length ? text[i + 2] : '\0';
            if (!char.IsUpper(nextChar) && !char.IsDigit(nextChar)) continue;

            return i + 1;
        }
        // Fall back: nearest whitespace
        for (var i = nearIndex; i > nearIndex - 100 && i >= 0; i--)
        {
            if (char.IsWhiteSpace(text[i])) return i;
        }
        return nearIndex;
    }
}
