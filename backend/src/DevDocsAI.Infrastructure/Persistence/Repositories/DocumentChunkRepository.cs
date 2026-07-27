using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocsAI.Infrastructure.Persistence.Repositories;

public sealed class DocumentChunkRepository(AppDbContext db) : IDocumentChunkRepository
{
    public async Task AddRangeAsync(IEnumerable<DocumentChunk> chunks, CancellationToken ct) =>
        await db.DocumentChunks.AddRangeAsync(chunks, ct);

    public Task DeleteByDocumentAsync(Guid documentId, CancellationToken ct) =>
        db.DocumentChunks.Where(c => c.DocumentId == documentId).ExecuteDeleteAsync(ct);

    public Task<int> CountByDocumentAsync(Guid documentId, CancellationToken ct) =>
        db.DocumentChunks.CountAsync(c => c.DocumentId == documentId, ct);

    public async Task<IReadOnlyList<DocumentChunk>> ListByDocumentAsync(Guid documentId, CancellationToken ct) =>
        await db.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentChunk>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.DocumentChunks.Where(c => ids.Contains(c.Id)).ToListAsync(ct);
}
