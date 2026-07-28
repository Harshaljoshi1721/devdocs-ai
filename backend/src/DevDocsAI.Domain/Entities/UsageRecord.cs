using DevDocsAI.Domain.Common;
using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// A metered AI operation: which user/project incurred it, its kind, and the
/// (estimated) token counts. Enables usage summaries and future cost tracking.
/// </summary>
public sealed class UsageRecord : Entity
{
    private UsageRecord() { } // EF

    private UsageRecord(Guid userId, Guid projectId, UsageKind kind, int tokensIn, int tokensOut)
    {
        UserId = userId;
        ProjectId = projectId;
        Kind = kind;
        TokensIn = tokensIn;
        TokensOut = tokensOut;
    }

    public Guid UserId { get; private set; }
    public Guid ProjectId { get; private set; }
    public UsageKind Kind { get; private set; }
    public int TokensIn { get; private set; }
    public int TokensOut { get; private set; }
    public decimal? CostEstimate { get; private set; }

    public static UsageRecord Create(Guid userId, Guid projectId, UsageKind kind, int tokensIn, int tokensOut) =>
        new(userId, projectId, kind, tokensIn, tokensOut);
}
