using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocsAI.Infrastructure.Persistence.Repositories;

public sealed class AgentRunRepository(AppDbContext db) : IAgentRunRepository
{
    public Task<AgentRun?> GetWithToolExecutionsAsync(Guid id, CancellationToken ct) =>
        db.AgentRuns
            .Include(r => r.ToolExecutions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<AgentRun>> ListByProjectAsync(Guid projectId, Guid userId, CancellationToken ct) =>
        await db.AgentRuns
            .Where(r => r.ProjectId == projectId && r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(AgentRun run, CancellationToken ct) =>
        await db.AgentRuns.AddAsync(run, ct);
}
