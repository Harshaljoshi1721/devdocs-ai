using DevDocsAI.Domain.Entities;
using DevDocsAI.Infrastructure.Vectors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevDocsAI.Infrastructure.Persistence.Configurations;

public sealed class ChunkEmbeddingConfiguration : IEntityTypeConfiguration<ChunkEmbeddingRecord>
{
    // Must match the embedding model's output dimension (Gemini text-embedding-004 = 768).
    private const int Dimensions = 768;

    public void Configure(EntityTypeBuilder<ChunkEmbeddingRecord> builder)
    {
        builder.ToTable("chunk_embeddings");
        builder.HasKey(e => e.ChunkId);
        builder.Property(e => e.Embedding).HasColumnType($"vector({Dimensions})");
        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.DocumentId);

        // Approximate-nearest-neighbour index for cosine similarity.
        builder.HasIndex(e => e.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        // Deleting a chunk removes its embedding.
        builder.HasOne<DocumentChunk>()
            .WithMany()
            .HasForeignKey(e => e.ChunkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
