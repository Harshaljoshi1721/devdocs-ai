using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace DevDocsAI.Infrastructure.Vectors;

/// <summary>pgvector-backed vector store: cosine-similarity search scoped per project.</summary>
public sealed class PgVectorStore(AppDbContext db) : IVectorStore
{
    public async Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken ct)
    {
        if (records.Count == 0)
        {
            return;
        }

        var ids = records.Select(r => r.ChunkId).ToList();
        await db.ChunkEmbeddings.Where(e => ids.Contains(e.ChunkId)).ExecuteDeleteAsync(ct);

        db.ChunkEmbeddings.AddRange(records.Select(r => new ChunkEmbeddingRecord
        {
            ChunkId = r.ChunkId,
            DocumentId = r.DocumentId,
            ProjectId = r.ProjectId,
            Embedding = new Vector(r.Embedding),
        }));

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        Guid projectId, float[] queryEmbedding, int topK, CancellationToken ct)
    {
        var query = new Vector(queryEmbedding);

        var hits = await db.ChunkEmbeddings
            .Where(e => e.ProjectId == projectId)
            .OrderBy(e => e.Embedding.CosineDistance(query))
            .Take(topK)
            .Select(e => new { e.ChunkId, Distance = e.Embedding.CosineDistance(query) })
            .ToListAsync(ct);

        // Cosine similarity = 1 - cosine distance (higher is closer).
        return hits.Select(h => new VectorSearchHit(h.ChunkId, 1.0 - h.Distance)).ToList();
    }

    public Task DeleteByDocumentAsync(Guid documentId, CancellationToken ct) =>
        db.ChunkEmbeddings.Where(e => e.DocumentId == documentId).ExecuteDeleteAsync(ct);
}
