using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevDocsAI.Infrastructure.Persistence.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");
        builder.HasKey(c => c.Id);
        // Ids are assigned in code (UUID v7), not by the store.
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Title).HasMaxLength(500).IsRequired();

        builder.HasIndex(c => c.ProjectId);
        builder.HasIndex(c => new { c.ProjectId, c.UserId });

        // A conversation lives inside a project; removing the project removes it.
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // The starting user is referenced but not a cascade source (the project
        // cascade already covers owner deletion; avoids ambiguous cascade paths).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Conversation.Messages))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
