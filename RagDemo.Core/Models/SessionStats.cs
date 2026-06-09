namespace RagDemo.Core.Models;

public record SessionStats(
    string SessionId,
    int PagesCrawled,
    int ChunksStored,
    List<string> SourceUrls,
    DateTime? LastIngestAt);
