namespace DevDocsAI.Domain.Enums;

/// <summary>Outcome of a single tool invocation within an agent run.</summary>
public enum ToolExecutionStatus
{
    Ok = 0,
    Error = 1,
}
