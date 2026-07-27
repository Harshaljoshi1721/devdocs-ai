using DevDocsAI.Domain.Entities;

namespace DevDocsAI.Application.Abstractions.Persistence;

public interface IDocumentChunkRepository
{
    Task AddRangeAsync(IEnumerable<DocumentChunk> chunks, CancellationToken ct);

    /// <summary>Removes all chunks for a document (immediate) so it can be reprocessed idempotently.</summary>
    Task DeleteByDocumentAsync(Guid documentId, CancellationToken ct);

    Task<int> CountByDocumentAsync(Guid documentId, CancellationToken ct);

    Task<IReadOnlyList<DocumentChunk>> ListByDocumentAsync(Guid documentId, CancellationToken ct);

    Task<IReadOnlyList<DocumentChunk>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
