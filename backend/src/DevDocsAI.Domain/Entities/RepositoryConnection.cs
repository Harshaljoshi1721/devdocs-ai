using DevDocsAI.Domain.Common;
using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// A project's link to an external source repository. Ingestion produces
/// <see cref="Document"/>s tagged with this connection's id. Status mirrors the
/// document processing lifecycle so the frontend can poll it.
/// </summary>
public sealed class RepositoryConnection : Entity
{
    private RepositoryConnection() { } // EF

    private RepositoryConnection(
        Guid projectId, RepositoryProvider provider, string url, string owner, string repo, string? @ref)
    {
        ProjectId = projectId;
        Provider = provider;
        Url = url;
        Owner = owner;
        Repo = repo;
        Ref = @ref;
        Status = ProcessingStatus.Pending;
    }

    public Guid ProjectId { get; private set; }
    public RepositoryProvider Provider { get; private set; }
    public string Url { get; private set; } = null!;
    public string Owner { get; private set; } = null!;
    public string Repo { get; private set; } = null!;

    /// <summary>Requested branch/tag; null means the repository's default branch.</summary>
    public string? Ref { get; private set; }

    /// <summary>Commit actually ingested; null until a run completes.</summary>
    public string? CommitSha { get; private set; }

    public ProcessingStatus Status { get; private set; }
    public string? Error { get; private set; }
    public int FileCount { get; private set; }

    public static RepositoryConnection Connect(
        Guid projectId, RepositoryProvider provider, string url, string owner, string repo, string? @ref) =>
        new(projectId, provider, url, owner, repo, @ref);

    public void MarkProcessing()
    {
        Status = ProcessingStatus.Processing;
        Error = null;
    }

    public void MarkCompleted(string commitSha, int fileCount)
    {
        Status = ProcessingStatus.Completed;
        CommitSha = commitSha;
        FileCount = fileCount;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Status = ProcessingStatus.Failed;
        Error = error;
    }

    /// <summary>Return to Pending before a re-sync, discarding the previous run's result.</summary>
    public void Reset()
    {
        Status = ProcessingStatus.Pending;
        CommitSha = null;
        FileCount = 0;
        Error = null;
    }
}
