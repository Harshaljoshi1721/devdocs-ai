using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevDocsAI.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Content).IsRequired();
        builder.Property(c => c.EmbeddingReference).HasMaxLength(256);
        builder.HasIndex(c => c.DocumentId);
        builder.HasIndex(c => new { c.DocumentId, c.ChunkIndex }).IsUnique();

        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
