using DevDocsAI.Domain.Common;
using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// One recorded tool invocation inside an <see cref="AgentRun"/>: what was called,
/// the JSON input, the observation returned, and how it went. The trace is the
/// project's audit of how an answer was produced.
/// </summary>
public sealed class ToolExecution : Entity
{
    private ToolExecution() { } // EF

    internal ToolExecution(
        Guid agentRunId, int sequence, string toolName, string inputJson,
        string outputJson, ToolExecutionStatus status, string? error, long durationMs)
    {
        AgentRunId = agentRunId;
        Sequence = sequence;
        ToolName = toolName;
        InputJson = inputJson;
        OutputJson = outputJson;
        Status = status;
        Error = error;
        DurationMs = durationMs;
    }

    public Guid AgentRunId { get; private set; }
    public int Sequence { get; private set; }
    public string ToolName { get; private set; } = null!;
    public string InputJson { get; private set; } = null!;
    public string OutputJson { get; private set; } = null!;
    public ToolExecutionStatus Status { get; private set; }
    public string? Error { get; private set; }
    public long DurationMs { get; private set; }
}
