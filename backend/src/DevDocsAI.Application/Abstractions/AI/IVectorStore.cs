namespace DevDocsAI.Application.Abstractions.AI;

/// <summary>A chunk's embedding plus the ids needed to scope and cite it.</summary>
public sealed record VectorRecord(Guid ChunkId, Guid DocumentId, Guid ProjectId, float[] Embedding);

/// <summary>A search hit: the chunk id and its similarity score (higher = closer).</summary>
public sealed record VectorSearchHit(Guid ChunkId, double Score);

/// <summary>
/// Stores and searches chunk embeddings. The default implementation is pgvector,
/// kept behind this port so a dedicated vector DB can be swapped in later.
/// </summary>
public interface IVectorStore
{
    Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken ct);

    /// <summary>Top-k nearest chunks within a project, by cosine similarity.</summary>
    Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        Guid projectId, float[] queryEmbedding, int topK, CancellationToken ct);

    Task DeleteByDocumentAsync(Guid documentId, CancellationToken ct);
}
