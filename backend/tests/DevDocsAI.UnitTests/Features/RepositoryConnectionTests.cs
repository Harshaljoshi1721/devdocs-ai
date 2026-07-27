using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class RepositoryConnectionTests
{
    private readonly Guid _projectId = Guid.CreateVersion7();

    [Fact]
    public void Connect_starts_pending_for_github()
    {
        var c = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/octo/cat", "octo", "cat", "main");

        c.ProjectId.ShouldBe(_projectId);
        c.Provider.ShouldBe(RepositoryProvider.GitHub);
        c.Owner.ShouldBe("octo");
        c.Repo.ShouldBe("cat");
        c.Ref.ShouldBe("main");
        c.Status.ShouldBe(ProcessingStatus.Pending);
        c.CommitSha.ShouldBeNull();
        c.FileCount.ShouldBe(0);
    }

    [Fact]
    public void Lifecycle_marks_processing_then_completed_with_commit_and_count()
    {
        var c = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/octo/cat", "octo", "cat", null);

        c.MarkProcessing();
        c.Status.ShouldBe(ProcessingStatus.Processing);

        c.MarkCompleted("abc123", 42);
        c.Status.ShouldBe(ProcessingStatus.Completed);
        c.CommitSha.ShouldBe("abc123");
        c.FileCount.ShouldBe(42);
        c.Error.ShouldBeNull();
    }

    [Fact]
    public void MarkFailed_records_the_error()
    {
        var c = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/octo/cat", "octo", "cat", null);

        c.MarkFailed("boom");

        c.Status.ShouldBe(ProcessingStatus.Failed);
        c.Error.ShouldBe("boom");
    }

    [Fact]
    public void Reset_returns_to_pending_and_clears_prior_result()
    {
        var c = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/octo/cat", "octo", "cat", null);
        c.MarkCompleted("abc123", 42);

        c.Reset();

        c.Status.ShouldBe(ProcessingStatus.Pending);
        c.CommitSha.ShouldBeNull();
        c.FileCount.ShouldBe(0);
        c.Error.ShouldBeNull();
    }
}
