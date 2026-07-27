namespace DevDocsAI.Application.Features.Ingestion;

/// <summary>A single file presented for upload. Content is read once by the service.</summary>
public sealed record UploadFileInput(string FileName, long Length, Stream Content);

public sealed record DocumentResponse(
    Guid Id,
    string Name,
    string Path,
    string FileType,
    long Size,
    string ContentHash,
    string Status,
    string? Error,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record RejectedFile(string FileName, string Reason);

/// <summary>Outcome of an upload: which files were accepted and which were rejected (and why).</summary>
public sealed record UploadResult(
    IReadOnlyList<DocumentResponse> Accepted,
    IReadOnlyList<RejectedFile> Rejected);
