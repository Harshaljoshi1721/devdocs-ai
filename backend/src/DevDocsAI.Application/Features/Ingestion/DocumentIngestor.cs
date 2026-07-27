using DevDocsAI.Application.Abstractions;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Abstractions.Storage;
using DevDocsAI.Application.Features.Processing;
using DevDocsAI.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace DevDocsAI.Application.Features.Ingestion;

/// <summary>Outcome of ingesting a single file: the created (unsaved) document, or a rejection reason.</summary>
public sealed record IngestOutcome(Document? Document, string? RejectionReason)
{
    public static IngestOutcome Accepted(Document document) => new(document, null);
    public static IngestOutcome Rejected(string reason) => new(null, reason);
}

/// <summary>
/// The per-file half of ingestion, shared by manual upload and repository import:
/// screen → store → content-hash dedupe → create a <see cref="Document"/> (added to
/// the unit of work, not yet saved). Callers batch the save and enqueue processing.
/// </summary>
public interface IDocumentIngestor
{
    Task<IngestOutcome> IngestAsync(
        Guid projectId, string path, long length, Stream content,
        Guid? repositoryConnectionId, ISet<string> seenHashes, CancellationToken ct);

    /// <summary>Enqueue the background chunk/embed pipeline for each accepted document.</summary>
    Task EnqueueProcessingAsync(IReadOnlyList<Guid> documentIds, CancellationToken ct);
}

public sealed class DocumentIngestor(
    IDocumentRepository documents,
    IFileStorage fileStorage,
    IFileFilter fileFilter,
    IBackgroundTaskQueue queue) : IDocumentIngestor
{
    public async Task<IngestOutcome> IngestAsync(
        Guid projectId, string path, long length, Stream content,
        Guid? repositoryConnectionId, ISet<string> seenHashes, CancellationToken ct)
    {
        if (length <= 0) return IngestOutcome.Rejected("empty");
        if (fileFilter.IsSecret(path)) return IngestOutcome.Rejected("secret");
        if (!fileFilter.IsSupported(path)) return IngestOutcome.Rejected("unsupported");

        var extension = Path.GetExtension(Path.GetFileName(path));
        var stored = await fileStorage.SaveAsync(projectId, extension, content, ct);

        if (!seenHashes.Add(stored.ContentHash) ||
            await documents.ExistsByHashAsync(projectId, stored.ContentHash, ct))
        {
            await fileStorage.DeleteAsync(stored.StorageKey, ct);
            return IngestOutcome.Rejected("duplicate");
        }

        var document = new Document(
            projectId,
            name: Path.GetFileName(path),
            path: path,
            fileType: fileFilter.Categorize(path),
            contentHash: stored.ContentHash,
            size: stored.SizeBytes,
            storageKey: stored.StorageKey,
            repositoryConnectionId: repositoryConnectionId);

        await documents.AddAsync(document, ct);
        return IngestOutcome.Accepted(document);
    }

    public async Task EnqueueProcessingAsync(IReadOnlyList<Guid> documentIds, CancellationToken ct)
    {
        foreach (var id in documentIds)
        {
            await queue.EnqueueAsync(
                (sp, token) => new ValueTask(sp.GetRequiredService<IDocumentProcessor>().ProcessAsync(id, token)),
                ct);
        }
    }
}
