using System.Text;
using System.Text.Json;
using DevDocsAI.Application.Features.Rag;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Application.Features.Agents.Tools;

/// <summary>Semantic search over the project's indexed chunks (reuses RAG retrieval).</summary>
public sealed class SearchProjectTool(IRetrievalService retrieval, IOptions<AgentOptions> options) : IAgentTool
{
    private readonly AgentOptions _options = options.Value;

    public string Name => ToolNames.SearchProject;
    public string Description =>
        "Semantic code/doc search. Arguments: { \"query\": string, \"topK\"?: number }. " +
        "Returns ranked snippets with file paths and line ranges.";

    public async Task<string> ExecuteAsync(Guid projectId, JsonElement arguments, CancellationToken ct)
    {
        var query = ToolArgs.RequireString(arguments, "query");
        var topK = ToolArgs.OptionalInt(arguments, "topK") ?? _options.SearchTopK;

        var hits = await retrieval.RetrieveAsync(projectId, query, topK, ct);
        if (hits.Count == 0) return "No matching content found.";

        var sb = new StringBuilder();
        var i = 1;
        foreach (var h in hits)
        {
            sb.AppendLine($"{i}. {h.Path}:{h.StartLine}-{h.EndLine}");
            sb.AppendLine(h.Snippet);
            sb.AppendLine();
            i++;
        }

        return sb.ToString().TrimEnd();
    }
}
