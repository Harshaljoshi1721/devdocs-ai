namespace DevDocsAI.Application.Features.Agents;

/// <summary>Bounds for agent runs, bound from the "Agent" config section.</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Maximum ReAct iterations before the run is stopped and marked failed.</summary>
    public int MaxIterations { get; init; } = 8;

    /// <summary>Maximum characters returned by ReadFile before truncation.</summary>
    public int MaxFileChars { get; init; } = 8000;

    /// <summary>Default number of results for SearchProject.</summary>
    public int SearchTopK { get; init; } = 6;
}
