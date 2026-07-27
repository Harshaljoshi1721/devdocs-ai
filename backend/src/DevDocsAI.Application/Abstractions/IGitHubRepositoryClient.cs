namespace DevDocsAI.Application.Abstractions;

/// <summary>A downloaded repository snapshot: the resolved commit and a gzip'd tar stream of the tree.</summary>
public sealed record RepositoryArchive(string CommitSha, Stream Content) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        if (Content is not null) await Content.DisposeAsync();
    }
}

/// <summary>Retrieves public repository contents. Provider-swappable (only GitHub in Phase 7).</summary>
public interface IGitHubRepositoryClient
{
    /// <summary>Downloads the repo at <paramref name="ref"/> (or the default branch) as a tar.gz stream.</summary>
    Task<RepositoryArchive> DownloadTarballAsync(string owner, string repo, string? @ref, CancellationToken ct);
}
