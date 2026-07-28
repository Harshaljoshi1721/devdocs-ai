using System.Text;
using System.Text.Json;
using DevDocsAI.Application.Abstractions.Persistence;

namespace DevDocsAI.Application.Features.Agents.Tools;

/// <summary>Lists the project's indexed files (path, type, status).</summary>
public sealed class GetProjectStructureTool(IDocumentRepository documents) : IAgentTool
{
    public string Name => ToolNames.GetProjectStructure;
    public string Description => "List all indexed files in the project. Arguments: {} (none).";

    public async Task<string> ExecuteAsync(Guid projectId, JsonElement arguments, CancellationToken ct)
    {
        var docs = await documents.ListByProjectAsync(projectId, ct);
        if (docs.Count == 0) return "The project has no indexed files yet.";

        var sb = new StringBuilder();
        sb.AppendLine($"{docs.Count} file(s):");
        foreach (var d in docs.OrderBy(d => d.Path, StringComparer.Ordinal))
            sb.AppendLine($"- {d.Path} ({d.FileType}, {d.ProcessingStatus})");

        return sb.ToString().TrimEnd();
    }
}
