namespace RagDemo.Core.Models;

public record IngestProgress(
    string Stage,
    string Message,
    int? PagesFound = null,
    int? PagesProcessed = null,
    int? ChunksStored = null,
    bool IsComplete = false,
    string? Error = null);
