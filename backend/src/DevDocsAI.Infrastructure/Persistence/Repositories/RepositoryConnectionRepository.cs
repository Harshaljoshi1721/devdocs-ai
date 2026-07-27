using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocsAI.Infrastructure.Persistence.Repositories;

public sealed class RepositoryConnectionRepository(AppDbContext db) : IRepositoryConnectionRepository
{
    public Task<RepositoryConnection?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.RepositoryConnections.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<RepositoryConnection?> GetByProjectAsync(Guid projectId, CancellationToken ct) =>
        db.RepositoryConnections.FirstOrDefaultAsync(c => c.ProjectId == projectId, ct);

    public async Task AddAsync(RepositoryConnection connection, CancellationToken ct) =>
        await db.RepositoryConnections.AddAsync(connection, ct);

    public void Remove(RepositoryConnection connection) => db.RepositoryConnections.Remove(connection);
}
