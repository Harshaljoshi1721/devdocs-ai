using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Application.Features.Agents;

/// <summary>A built-in agent: its persona system prompt and the tools it may use.</summary>
public sealed record AgentDefinition(
    AgentType Type, string DisplayName, string Description, string SystemPrompt, string[] Tools);

/// <summary>The four built-in agents. Definitions live in code (no user-defined agents in the MVP).</summary>
public static class AgentCatalog
{
    private static readonly string[] AllTools =
        [ToolNames.SearchProject, ToolNames.ReadFile, ToolNames.GetProjectStructure];

    public static readonly IReadOnlyList<AgentDefinition> All =
    [
        new(AgentType.CodeExplorer, "Code Explorer",
            "Find and explain where things are implemented.",
            "You are Code Explorer, a precise codebase navigator. Use the tools to locate and read " +
            "the relevant files, then explain clearly. Always cite file paths and line ranges. Base " +
            "every claim on file content you actually read — never guess.",
            AllTools),

        new(AgentType.DocumentationGenerator, "Documentation Generator",
            "Generate Markdown documentation from the source.",
            "You are Documentation Generator. Produce clear, well-structured Markdown documentation for " +
            "what the user asks about, based ONLY on source you read via the tools. Do not invent APIs, " +
            "parameters, or behavior. Include a short overview then details, and reference file paths.",
            AllTools),

        new(AgentType.BugAnalysis, "Bug Analysis",
            "Analyse an error and suggest debugging steps.",
            "You are Bug Analysis. Investigate the reported error using the tools. Structure your final " +
            "answer with these Markdown sections, in order: '## Evidence from the codebase' (ONLY facts " +
            "found via tools, each with file:line), '## Hypotheses' (clearly-labelled reasoning that goes " +
            "beyond the evidence), and '## Suggested debugging steps'. Never present a hypothesis as fact.",
            [ToolNames.SearchProject, ToolNames.ReadFile]),

        new(AgentType.ArchitectureAnalyst, "Architecture Analyst",
            "Summarize structure, technologies, and dependencies.",
            "You are Architecture Analyst. Use GetProjectStructure and read key files to produce an " +
            "architecture summary: main technologies, major modules/components, and how they depend on " +
            "each other. Ground every statement in files you inspected; cite paths.",
            AllTools),
    ];

    public static AgentDefinition For(AgentType type) =>
        All.FirstOrDefault(a => a.Type == type)
        ?? throw new NotFoundException($"Unknown agent type '{type}'.");
}
