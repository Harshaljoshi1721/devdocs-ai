using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;

namespace DevDocsAI.Application.Features.Usage;

public sealed record UsageByKind(string Kind, int Requests, long TokensIn, long TokensOut);

public sealed record UsageSummaryResponse(
    int TotalRequests, long TotalTokensIn, long TotalTokensOut, IReadOnlyList<UsageByKind> ByKind);

public interface IUsageService
{
    Task<UsageSummaryResponse> SummarizeAsync(Guid userId, Guid projectId, CancellationToken ct);
}

public sealed class UsageService(IProjectRepository projects, IUsageRecordRepository usage) : IUsageService
{
    public async Task<UsageSummaryResponse> SummarizeAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null || project.OwnerId != userId)
        {
            throw new NotFoundException("Project not found.");
        }

        var records = await usage.ListByProjectAsync(projectId, ct);

        var byKind = records
            .GroupBy(r => r.Kind)
            .Select(g => new UsageByKind(
                g.Key.ToString(), g.Count(), g.Sum(r => (long)r.TokensIn), g.Sum(r => (long)r.TokensOut)))
            .OrderBy(k => k.Kind, StringComparer.Ordinal)
            .ToList();

        return new UsageSummaryResponse(
            records.Count, records.Sum(r => (long)r.TokensIn), records.Sum(r => (long)r.TokensOut), byKind);
    }
}
