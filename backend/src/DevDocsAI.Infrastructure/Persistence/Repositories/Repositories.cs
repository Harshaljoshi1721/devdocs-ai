using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocsAI.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);

    public async Task AddAsync(User user, CancellationToken ct) =>
        await db.Users.AddAsync(user, ct);
}

public sealed class ProjectRepository(AppDbContext db) : IProjectRepository
{
    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Project>> ListByOwnerAsync(Guid ownerId, CancellationToken ct) =>
        await db.Projects
            .Where(p => p.OwnerId == ownerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Project project, CancellationToken ct) =>
        await db.Projects.AddAsync(project, ct);

    public void Remove(Project project) => db.Projects.Remove(project);
}

public sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct) =>
        await db.RefreshTokens.AddAsync(token, ct);
}
