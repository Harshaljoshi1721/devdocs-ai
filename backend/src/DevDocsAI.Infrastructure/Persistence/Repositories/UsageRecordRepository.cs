using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocsAI.Infrastructure.Persistence.Repositories;

public sealed class UsageRecordRepository(AppDbContext db) : IUsageRecordRepository
{
    public async Task AddAsync(UsageRecord record, CancellationToken ct) =>
        await db.UsageRecords.AddAsync(record, ct);

    public async Task<IReadOnlyList<UsageRecord>> ListByProjectAsync(Guid projectId, CancellationToken ct) =>
        await db.UsageRecords.Where(r => r.ProjectId == projectId).ToListAsync(ct);
}
