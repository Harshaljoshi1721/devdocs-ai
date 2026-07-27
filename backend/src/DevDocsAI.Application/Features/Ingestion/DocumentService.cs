using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Abstractions.Storage;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Domain.Entities;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Application.Features.Ingestion;

public interface IDocumentService
{
    Task<UploadResult> UploadAsync(Guid userId, Guid projectId, IReadOnlyList<UploadFileInput> files, CancellationToken ct);
    Task<IReadOnlyList<DocumentResponse>> ListAsync(Guid userId, Guid projectId, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid projectId, Guid documentId, CancellationToken ct);

    /// <summary>Removes all documents ingested from a repository connection (used by re-sync/disconnect).</summary>
    Task RemoveByConnectionAsync(Guid repositoryConnectionId, CancellationToken ct);
}

public sealed class DocumentService(
    IProjectRepository projects,
    IDocumentRepository documents,
    IFileStorage fileStorage,
    IDocumentIngestor ingestor,
    IUnitOfWork unitOfWork,
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
            if (file.Length > _options.MaxFileSizeBytes)
            {
                rejected.Add(new RejectedFile(file.FileName, "too_large"));
                continue;
            }

            var outcome = await ingestor.IngestAsync(
                projectId, file.FileName, file.Length, file.Content, null, seenHashes, ct);

            if (outcome.DocumentId is { } id)
            {
                var doc = await documents.GetByIdAsync(id, ct);
                accepted.Add(Map(doc!));
                acceptedIds.Add(id);
            }
            else
            {
                rejected.Add(new RejectedFile(file.FileName, outcome.RejectionReason!));
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        await ingestor.EnqueueProcessingAsync(acceptedIds, ct);

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

    public async Task RemoveByConnectionAsync(Guid repositoryConnectionId, CancellationToken ct)
    {
        var docs = await documents.ListByConnectionAsync(repositoryConnectionId, ct);
        if (docs.Count == 0) return;

        foreach (var doc in docs)
        {
            documents.Remove(doc);
        }

        await unitOfWork.SaveChangesAsync(ct);

        foreach (var doc in docs)
        {
            await fileStorage.DeleteAsync(doc.StorageKey, ct);
        }
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
