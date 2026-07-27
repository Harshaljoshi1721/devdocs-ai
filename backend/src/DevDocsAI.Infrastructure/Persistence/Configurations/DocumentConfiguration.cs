using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevDocsAI.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(500).IsRequired();
        builder.Property(d => d.Path).HasMaxLength(1024).IsRequired();
        builder.Property(d => d.FileType).HasConversion<string>().HasMaxLength(32);
        builder.Property(d => d.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(d => d.StorageKey).HasMaxLength(256).IsRequired();
        builder.Property(d => d.ProcessingStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(d => d.Error).HasMaxLength(4000);

        builder.HasIndex(d => d.ProjectId);
        builder.HasIndex(d => new { d.ProjectId, d.ContentHash });

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(d => d.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
