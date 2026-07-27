namespace DevDocsAI.Application.Features.Repositories;

/// <summary>Caps applied when ingesting a repository, bound from the "RepoIngestion" section.</summary>
public sealed class RepoIngestionOptions
{
    public const string SectionName = "RepoIngestion";

    /// <summary>Maximum number of supported files ingested from one repository.</summary>
    public int MaxFiles { get; init; } = 1500;

    /// <summary>Maximum total decompressed bytes read before aborting (tar-bomb guard). 25 MB.</summary>
    public long MaxTotalBytes { get; init; } = 25L * 1024 * 1024;

    /// <summary>Maximum size of a single file. 5 MB (matches the upload cap).</summary>
    public long MaxFileBytes { get; init; } = 5L * 1024 * 1024;
}
