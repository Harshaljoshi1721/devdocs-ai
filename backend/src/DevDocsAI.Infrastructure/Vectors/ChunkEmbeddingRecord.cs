using Pgvector;

namespace DevDocsAI.Infrastructure.Vectors;

/// <summary>
/// Infrastructure-only persistence type for a chunk's embedding. It lives here
/// (not in Domain) so the pgvector <see cref="Vector"/> type never leaks into
/// the Domain layer.
/// </summary>
public sealed class ChunkEmbeddingRecord
{
    public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid ProjectId { get; set; }
    public Vector Embedding { get; set; } = null!;
}
