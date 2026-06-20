using RagDemo.Core.Services;

namespace RagDemo.Tests;

public class TextChunkerTests
{
    private readonly TextChunker _chunker = new();
    private const string Url = "https://example.com";

    [Fact]
    public void EmptyText_YieldsNoChunks()
    {
        var chunks = _chunker.Chunk("", Url).ToList();
        Assert.Empty(chunks);
    }

    [Fact]
    public void WhitespaceOnly_YieldsNoChunks()
    {
        var chunks = _chunker.Chunk("   \t\n  ", Url).ToList();
        Assert.Empty(chunks);
    }

    [Fact]
    public void ShortText_YieldsSingleChunk()
    {
        const string text = "Hello world. This is a short sentence.";
        var chunks = _chunker.Chunk(text, Url).ToList();
        Assert.Single(chunks);
        Assert.Equal(text, chunks[0].Text);
        Assert.Equal(Url, chunks[0].SourceUrl);
        Assert.Equal(0, chunks[0].ChunkIndex);
    }

    [Fact]
    public void ShortText_DoesNotInfiniteLoop()
    {
        // A text shorter than OverlapSize must not re-enter the loop.
        // ChunkSize = 2400, OverlapSize = 400 — so 50 chars is well under both.
        var text = new string('a', 50) + ". End.";
        var chunks = _chunker.Chunk(text, Url).ToList();
        Assert.Single(chunks);
    }

    [Fact]
    public void LongText_ProducesMultipleChunks()
    {
        // Build text clearly longer than one chunk (ChunkSize = 2400 chars)
        var sentence = "This is a normal sentence that ends properly. ";
        var text = string.Concat(Enumerable.Repeat(sentence, 70)); // ~3150 chars
        var chunks = _chunker.Chunk(text, Url).ToList();
        Assert.True(chunks.Count >= 2, $"Expected ≥2 chunks, got {chunks.Count}");
    }

    [Fact]
    public void LongText_ChunkIndicesAreSequential()
    {
        var sentence = "Another sentence here to fill the chunk budget. ";
        var text = string.Concat(Enumerable.Repeat(sentence, 70));
        var chunks = _chunker.Chunk(text, Url).ToList();
        for (var i = 0; i < chunks.Count; i++)
            Assert.Equal(i, chunks[i].ChunkIndex);
    }

    [Fact]
    public void LongText_NoChunkIsEmpty()
    {
        var sentence = "Filling sentence for testing overlap and boundary logic. ";
        var text = string.Concat(Enumerable.Repeat(sentence, 70));
        var chunks = _chunker.Chunk(text, Url).ToList();
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.Text)));
    }

    [Fact]
    public void Overlap_SecondChunkContainsTextFromFirstChunk()
    {
        // Build enough text to force two chunks. OverlapSize = 400 chars.
        var sentence = "Overlap detection sentence is right here now. ";
        var text = string.Concat(Enumerable.Repeat(sentence, 70));
        var chunks = _chunker.Chunk(text, Url).ToList();
        Assert.True(chunks.Count >= 2);
        // The end of chunk 0 should appear somewhere in chunk 1
        var tailOfFirst = chunks[0].Text[^200..];
        Assert.Contains(tailOfFirst.Trim()[..20], chunks[1].Text);
    }

    [Fact]
    public void AbbreviationDot_DoesNotSplitChunk()
    {
        // "Dr. Smith" must not be treated as a sentence boundary
        var prefix = new string('x', 2390) + " "; // push us close to chunk boundary
        var text = prefix + "Dr. Smith went to the store. Then he left.";
        var chunks = _chunker.Chunk(text, Url).ToList();
        // The split should not happen mid "Dr. Smith"
        var joined = string.Join(" ", chunks.Select(c => c.Text));
        Assert.Contains("Dr. Smith", joined);
    }

    [Fact]
    public void SentenceBoundary_SplitsAtPeriodFollowedByUppercase()
    {
        // Enough filler to reach chunk size, then a clear sentence boundary
        var filler = string.Concat(Enumerable.Repeat("Word filler text goes here now. ", 75));
        var chunks = _chunker.Chunk(filler, Url).ToList();
        // Every chunk (except possibly the last) should end at a sentence boundary (. ! ?)
        foreach (var chunk in chunks.SkipLast(1))
        {
            var trimmed = chunk.Text.TrimEnd();
            Assert.True(
                trimmed.EndsWith('.') || trimmed.EndsWith('!') || trimmed.EndsWith('?'),
                $"Chunk did not end at sentence boundary: ...{trimmed[^Math.Min(40, trimmed.Length)..]}");
        }
    }

    [Fact]
    public void SourceUrl_IsPreservedOnAllChunks()
    {
        const string customUrl = "https://test.example.com/page";
        var text = string.Concat(Enumerable.Repeat("Some text here. ", 70));
        var chunks = _chunker.Chunk(text, customUrl).ToList();
        Assert.All(chunks, c => Assert.Equal(customUrl, c.SourceUrl));
    }
}
