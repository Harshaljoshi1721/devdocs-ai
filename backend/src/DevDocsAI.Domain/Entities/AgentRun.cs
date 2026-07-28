using DevDocsAI.Domain.Common;
using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// A single execution of an agent against a project. Aggregate root for its
/// <see cref="ToolExecution"/> trace; tool executions are only created through
/// <see cref="AddToolExecution"/> so they stay consistent.
/// </summary>
public sealed class AgentRun : Entity
{
    private readonly List<ToolExecution> _toolExecutions = [];

    private AgentRun() { } // EF

    private AgentRun(Guid projectId, Guid userId, AgentType agentType, string input)
    {
        ProjectId = projectId;
        UserId = userId;
        AgentType = agentType;
        Input = input;
        Status = ProcessingStatus.Processing;
    }

    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public AgentType AgentType { get; private set; }
    public string Input { get; private set; } = null!;
    public string? Output { get; private set; }
    public ProcessingStatus Status { get; private set; }
    public string? Error { get; private set; }
    public int Iterations { get; private set; }

    public IReadOnlyList<ToolExecution> ToolExecutions => _toolExecutions.AsReadOnly();

    public static AgentRun Start(Guid projectId, Guid userId, AgentType agentType, string input) =>
        new(projectId, userId, agentType, input);

    public ToolExecution AddToolExecution(
        int sequence, string toolName, string inputJson, string outputJson,
        ToolExecutionStatus status, string? error, long durationMs)
    {
        var te = new ToolExecution(Id, sequence, toolName, inputJson, outputJson, status, error, durationMs);
        _toolExecutions.Add(te);
        return te;
    }

    public void Complete(string output, int iterations)
    {
        Output = output;
        Iterations = iterations;
        Status = ProcessingStatus.Completed;
        Error = null;
    }

    public void Fail(string error, int iterations)
    {
        Error = error;
        Iterations = iterations;
        Status = ProcessingStatus.Failed;
    }
}
