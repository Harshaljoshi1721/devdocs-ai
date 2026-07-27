using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevDocsAI.Infrastructure.Persistence.Configurations;

public sealed class RepositoryConnectionConfiguration : IEntityTypeConfiguration<RepositoryConnection>
{
    public void Configure(EntityTypeBuilder<RepositoryConnection> builder)
    {
        builder.ToTable("repository_connections");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever(); // app-assigned UUID v7

        builder.Property(c => c.Provider).HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.Url).HasMaxLength(2048).IsRequired();
        builder.Property(c => c.Owner).HasMaxLength(256).IsRequired();
        builder.Property(c => c.Repo).HasMaxLength(256).IsRequired();
        builder.Property(c => c.Ref).HasMaxLength(256);
        builder.Property(c => c.CommitSha).HasMaxLength(64);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.Error).HasMaxLength(4000);

        // One connection per project.
        builder.HasIndex(c => c.ProjectId).IsUnique();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
