using DevDocsAI.Domain.Common;
using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// An ingested file belonging to a project. The raw bytes live in file storage
/// (referenced by <see cref="StorageKey"/>); this row carries the metadata and
/// tracks the processing lifecycle. Chunks (Phase 4+) reference this document.
/// </summary>
public sealed class Document : Entity
{
    private Document() { } // EF

    public Document(
        Guid projectId,
        string name,
        string path,
        FileType fileType,
        string contentHash,
        long size,
        string storageKey)
    {
        ProjectId = projectId;
        Name = name;
        Path = path;
        FileType = fileType;
        ContentHash = contentHash;
        Size = size;
        StorageKey = storageKey;
        ProcessingStatus = ProcessingStatus.Pending;
    }

    public Guid ProjectId { get; private set; }

    /// <summary>Display file name (sanitized basename of the uploaded file).</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Original relative path as provided at upload (metadata only).</summary>
    public string Path { get; private set; } = null!;

    public FileType FileType { get; private set; }
    public string ContentHash { get; private set; } = null!;
    public long Size { get; private set; }

    /// <summary>Opaque key used to retrieve the bytes from file storage.</summary>
    public string StorageKey { get; private set; } = null!;

    public ProcessingStatus ProcessingStatus { get; private set; }
    public string? Error { get; private set; }

    public void MarkProcessing()
    {
        ProcessingStatus = ProcessingStatus.Processing;
        Error = null;
    }

    public void MarkCompleted()
    {
        ProcessingStatus = ProcessingStatus.Completed;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        ProcessingStatus = ProcessingStatus.Failed;
        Error = error;
    }
}
