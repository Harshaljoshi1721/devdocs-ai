using DevDocsAI.Application.Abstractions;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Abstractions.Storage;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Processing;
using DevDocsAI.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Application.Features.Ingestion;

public interface IDocumentService
{
    Task<UploadResult> UploadAsync(Guid userId, Guid projectId, IReadOnlyList<UploadFileInput> files, CancellationToken ct);
    Task<IReadOnlyList<DocumentResponse>> ListAsync(Guid userId, Guid projectId, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid projectId, Guid documentId, CancellationToken ct);
}

public sealed class DocumentService(
    IProjectRepository projects,
    IDocumentRepository documents,
    IFileStorage fileStorage,
    IFileFilter fileFilter,
    IUnitOfWork unitOfWork,
    IBackgroundTaskQueue queue,
    IOptions<UploadOptions> uploadOptions) : IDocumentService
{
    private readonly UploadOptions _options = uploadOptions.Value;

    public async Task<UploadResult> UploadAsync(
        Guid userId, Guid projectId, IReadOnlyList<UploadFileInput> files, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);

        if (files.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["files"] = ["At least one file is required."],
            });
        }

        if (files.Count > _options.MaxFilesPerRequest)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["files"] = [$"A maximum of {_options.MaxFilesPerRequest} files may be uploaded at once."],
            });
        }

        var accepted = new List<DocumentResponse>();
        var rejected = new List<RejectedFile>();
        var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var acceptedIds = new List<Guid>();

        foreach (var file in files)
        {
            var reason = ScreenMetadata(file);
            if (reason is not null)
            {
                rejected.Add(new RejectedFile(file.FileName, reason));
                continue;
            }

            var extension = Path.GetExtension(Path.GetFileName(file.FileName));
            var stored = await fileStorage.SaveAsync(projectId, extension, file.Content, ct);

            // Skip content already ingested for this project — whether persisted
            // previously or already accepted earlier in this same request.
            if (!seenHashes.Add(stored.ContentHash) ||
                await documents.ExistsByHashAsync(projectId, stored.ContentHash, ct))
            {
                await fileStorage.DeleteAsync(stored.StorageKey, ct);
                rejected.Add(new RejectedFile(file.FileName, "duplicate"));
                continue;
            }

            var document = new Document(
                projectId,
                name: Path.GetFileName(file.FileName),
                path: file.FileName,
                fileType: fileFilter.Categorize(file.FileName),
                contentHash: stored.ContentHash,
                size: stored.SizeBytes,
                storageKey: stored.StorageKey);

            await documents.AddAsync(document, ct);
            accepted.Add(Map(document));
            acceptedIds.Add(document.Id);
        }

        await unitOfWork.SaveChangesAsync(ct);

        // Hand each accepted document off to the background pipeline (Pending → Processing → Completed).
        foreach (var id in acceptedIds)
        {
            await queue.EnqueueAsync(
                (sp, token) => new ValueTask(sp.GetRequiredService<IDocumentProcessor>().ProcessAsync(id, token)),
                ct);
        }

        return new UploadResult(accepted, rejected);
    }

    public async Task<IReadOnlyList<DocumentResponse>> ListAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);
        var docs = await documents.ListByProjectAsync(projectId, ct);
        return docs.Select(Map).ToList();
    }

    public async Task DeleteAsync(Guid userId, Guid projectId, Guid documentId, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);

        var document = await documents.GetByIdAsync(documentId, ct);
        if (document is null || document.ProjectId != projectId)
        {
            throw new NotFoundException("Document not found.");
        }

        documents.Remove(document);
        await unitOfWork.SaveChangesAsync(ct);
        await fileStorage.DeleteAsync(document.StorageKey, ct);
    }

    /// <summary>Returns a rejection reason for a file, or null if it passes screening.</summary>
    private string? ScreenMetadata(UploadFileInput file)
    {
        if (file.Length <= 0) return "empty";
        if (file.Length > _options.MaxFileSizeBytes) return "too_large";
        if (fileFilter.IsSecret(file.FileName)) return "secret";
        if (!fileFilter.IsSupported(file.FileName)) return "unsupported";
        return null;
    }

    private async Task EnsureProjectOwnedAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null || project.OwnerId != userId)
        {
            throw new NotFoundException("Project not found.");
        }
    }

    private static DocumentResponse Map(Document d) => new(
        d.Id, d.Name, d.Path, d.FileType.ToString(), d.Size, d.ContentHash,
        d.ProcessingStatus.ToString(), d.Error, d.CreatedAt, d.UpdatedAt);
}
