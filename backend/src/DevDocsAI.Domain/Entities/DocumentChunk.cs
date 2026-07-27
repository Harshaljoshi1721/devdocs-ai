using DevDocsAI.Domain.Common;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// A contiguous slice of a document's text, carrying the source line range so
/// answers can cite exact locations. <see cref="EmbeddingReference"/> links the
/// chunk to its stored vector once embeddings are generated (Phase 5).
/// </summary>
public sealed class DocumentChunk : Entity
{
    private DocumentChunk() { } // EF

    public DocumentChunk(Guid documentId, int chunkIndex, string content, int startLine, int endLine)
    {
        DocumentId = documentId;
        ChunkIndex = chunkIndex;
        Content = content;
        StartLine = startLine;
        EndLine = endLine;
    }

    public Guid DocumentId { get; private set; }
    public int ChunkIndex { get; private set; }
    public string Content { get; private set; } = null!;

    /// <summary>1-based, inclusive source line range within the original file.</summary>
    public int StartLine { get; private set; }
    public int EndLine { get; private set; }

    public string? EmbeddingReference { get; private set; }

    public void SetEmbeddingReference(string reference) => EmbeddingReference = reference;
}
