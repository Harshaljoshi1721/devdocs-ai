namespace DevDocsAI.Application.Features.Rag;

public sealed record SearchRequest(string Query, int? TopK);

/// <summary>A retrieved chunk with its source location and relevance score.</summary>
public sealed record SearchHit(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string Path,
    int StartLine,
    int EndLine,
    double Score,
    string Snippet);

public sealed record SearchResponse(string Query, IReadOnlyList<SearchHit> Results);

public sealed record AskRequest(string Question, int? TopK);

/// <summary>A cited source used to ground an answer.</summary>
public sealed record Citation(
    Guid DocumentId,
    string DocumentName,
    string Path,
    int StartLine,
    int EndLine);

public sealed record AskResponse(string Answer, IReadOnlyList<Citation> Sources, bool Grounded);
