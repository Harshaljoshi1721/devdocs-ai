namespace DevDocsAI.Application.Features.Agents;

public sealed record AgentRunRequest(string Input);

public sealed record AgentInfo(string Type, string DisplayName, string Description);

public sealed record TraceItem(
    int Sequence, string ToolName, string Input, string Output, string Status, string? Error, long DurationMs);

public sealed record AgentRunResponse(
    Guid Id,
    string AgentType,
    string Status,
    string? Output,
    string? Error,
    int Iterations,
    IReadOnlyList<TraceItem> Trace,
    DateTime CreatedAt);

public sealed record AgentRunSummary(
    Guid Id, string AgentType, string Status, int Iterations, DateTime CreatedAt);
