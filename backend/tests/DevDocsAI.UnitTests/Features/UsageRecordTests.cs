using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class UsageRecordTests
{
    [Fact]
    public void Create_sets_all_fields()
    {
        var userId = Guid.CreateVersion7();
        var projectId = Guid.CreateVersion7();

        var record = UsageRecord.Create(userId, projectId, UsageKind.Chat, 120, 45);

        record.UserId.ShouldBe(userId);
        record.ProjectId.ShouldBe(projectId);
        record.Kind.ShouldBe(UsageKind.Chat);
        record.TokensIn.ShouldBe(120);
        record.TokensOut.ShouldBe(45);
        record.CostEstimate.ShouldBeNull();
    }
}
