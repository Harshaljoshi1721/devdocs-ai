using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevDocsAI.Infrastructure.Persistence.Configurations;

public sealed class ToolExecutionConfiguration : IEntityTypeConfiguration<ToolExecution>
{
    public void Configure(EntityTypeBuilder<ToolExecution> builder)
    {
        builder.ToTable("tool_executions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.ToolName).HasMaxLength(128).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(t => t.InputJson).IsRequired();
        builder.Property(t => t.OutputJson).IsRequired();
        builder.Property(t => t.Error).HasMaxLength(4000);
        builder.HasIndex(t => t.AgentRunId);
    }
}
