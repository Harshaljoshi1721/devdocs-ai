namespace DevDocsAI.Application.Features.Ingestion;

/// <summary>Limits applied to file uploads, bound from the "Upload" config section.</summary>
public sealed class UploadOptions
{
    public const string SectionName = "Upload";

    /// <summary>Maximum size of a single file, in bytes. Default 5 MB.</summary>
    public long MaxFileSizeBytes { get; init; } = 5 * 1024 * 1024;

    /// <summary>Maximum number of files accepted in one request.</summary>
    public int MaxFilesPerRequest { get; init; } = 50;
}
