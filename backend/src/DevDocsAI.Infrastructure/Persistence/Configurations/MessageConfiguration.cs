using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevDocsAI.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(m => m.Id);
        // App-assigned UUID v7 key: without this EF treats a set key as an existing
        // row and issues UPDATE (not INSERT) for messages added to a tracked conversation.
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(32);
        builder.Property(m => m.Content).IsRequired();
        builder.HasIndex(m => m.ConversationId);

        // Citations are grounding metadata, not first-class rows: store them as a
        // JSON column on the message (read-only collection backed by a field).
        builder.OwnsMany(m => m.Citations, nav => nav.ToJson());
        builder.Navigation(m => m.Citations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
