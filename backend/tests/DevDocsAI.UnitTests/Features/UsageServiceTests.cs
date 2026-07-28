using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Usage;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class UsageServiceTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IUsageRecordRepository _repo = Substitute.For<IUsageRecordRepository>();
    private readonly UsageService _sut;
    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly Guid _projectId = Guid.CreateVersion7();

    public UsageServiceTests()
    {
        _sut = new UsageService(_projects, _repo);
        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, _userId));
    }

    [Fact]
    public async Task Summarize_aggregates_totals_and_by_kind()
    {
        _repo.ListByProjectAsync(_projectId, Arg.Any<CancellationToken>()).Returns(new List<UsageRecord>
        {
            UsageRecord.Create(_userId, _projectId, UsageKind.Chat, 100, 20),
            UsageRecord.Create(_userId, _projectId, UsageKind.Chat, 50, 10),
            UsageRecord.Create(_userId, _projectId, UsageKind.Ask, 30, 5),
        });

        var summary = await _sut.SummarizeAsync(_userId, _projectId, default);

        summary.TotalRequests.ShouldBe(3);
        summary.TotalTokensIn.ShouldBe(180);
        summary.TotalTokensOut.ShouldBe(35);
        summary.ByKind.ShouldContain(k => k.Kind == "Chat" && k.Requests == 2);
    }

    [Fact]
    public async Task Summarize_on_another_users_project_is_not_found()
    {
        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, Guid.CreateVersion7()));

        await Should.ThrowAsync<NotFoundException>(() => _sut.SummarizeAsync(_userId, _projectId, default));
    }
}
