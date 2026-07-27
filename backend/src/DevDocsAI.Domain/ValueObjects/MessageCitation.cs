namespace DevDocsAI.Domain.ValueObjects;

/// <summary>
/// A source location an assistant answer was grounded in: the originating
/// document and the 1-based, inclusive line range within it. Persisted as JSON
/// on the owning <see cref="Entities.Message"/>.
/// </summary>
public sealed record MessageCitation(
    Guid DocumentId,
    string DocumentName,
    string Path,
    int StartLine,
    int EndLine);
