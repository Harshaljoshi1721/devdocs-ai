using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DevDocsAI.Application.Features.Usage;

/// <summary>Records AI usage. Best-effort: it must never break the user's request.</summary>
public interface IUsageRecorder
{
    Task RecordAsync(Guid userId, Guid projectId, UsageKind kind, int tokensIn, int tokensOut, CancellationToken ct);
}

public sealed class UsageRecorder(
    IUsageRecordRepository repository, IUnitOfWork unitOfWork, ILogger<UsageRecorder> logger) : IUsageRecorder
{
    public async Task RecordAsync(
        Guid userId, Guid projectId, UsageKind kind, int tokensIn, int tokensOut, CancellationToken ct)
    {
        try
        {
            await repository.AddAsync(UsageRecord.Create(userId, projectId, kind, tokensIn, tokensOut), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to record {Kind} usage for user {UserId} in project {ProjectId}",
                kind, userId, projectId);
        }
    }
}
