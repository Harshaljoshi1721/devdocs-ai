using DevDocsAI.Domain.Entities;

namespace DevDocsAI.Application.Abstractions.Persistence;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Document>> ListByProjectAsync(Guid projectId, CancellationToken ct);
    Task<IReadOnlyList<Document>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<Document>> ListByConnectionAsync(Guid repositoryConnectionId, CancellationToken ct);
    Task<bool> ExistsByHashAsync(Guid projectId, string contentHash, CancellationToken ct);
    Task AddAsync(Document document, CancellationToken ct);
    void Remove(Document document);
}
