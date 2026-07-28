using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocsAI.Infrastructure.Persistence.Repositories;

public sealed class DocumentRepository(AppDbContext db) : IDocumentRepository
{
    public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Document>> ListByProjectAsync(Guid projectId, CancellationToken ct) =>
        await db.Documents
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Document>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Documents.Where(d => ids.Contains(d.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<Document>> ListByConnectionAsync(Guid repositoryConnectionId, CancellationToken ct) =>
        await db.Documents.Where(d => d.RepositoryConnectionId == repositoryConnectionId).ToListAsync(ct);

    public Task<Document?> GetByPathAsync(Guid projectId, string path, CancellationToken ct) =>
        db.Documents
            .Where(d => d.ProjectId == projectId && d.Path == path)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<bool> ExistsByHashAsync(Guid projectId, string contentHash, CancellationToken ct) =>
        db.Documents.AnyAsync(d => d.ProjectId == projectId && d.ContentHash == contentHash, ct);

    public async Task AddAsync(Document document, CancellationToken ct) =>
        await db.Documents.AddAsync(document, ct);

    public void Remove(Document document) => db.Documents.Remove(document);
}
