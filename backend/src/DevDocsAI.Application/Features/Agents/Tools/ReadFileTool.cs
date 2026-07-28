using System.Text;
using System.Text.Json;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Application.Features.Agents.Tools;

/// <summary>Reads an indexed file's text by its project-relative path, with line numbers.</summary>
public sealed class ReadFileTool(
    IDocumentRepository documents, IFileStorage fileStorage, IOptions<AgentOptions> options) : IAgentTool
{
    private readonly AgentOptions _options = options.Value;

    public string Name => ToolNames.ReadFile;
    public string Description =>
        "Read one indexed file. Arguments: { \"path\": string } (use a path from SearchProject or " +
        "GetProjectStructure). Returns line-numbered content.";

    public async Task<string> ExecuteAsync(Guid projectId, JsonElement arguments, CancellationToken ct)
    {
        var path = ToolArgs.RequireString(arguments, "path");
        var doc = await documents.GetByPathAsync(projectId, path, ct);
        if (doc is null) return $"File not found: {path}";

        await using var stream = await fileStorage.OpenReadAsync(doc.StorageKey, ct);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(ct);

        var truncated = content.Length > _options.MaxFileChars;
        if (truncated) content = content[.._options.MaxFileChars];

        var lines = content.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        sb.AppendLine($"File: {path}");
        for (var i = 0; i < lines.Length; i++)
            sb.AppendLine($"{i + 1}\t{lines[i]}");
        if (truncated) sb.AppendLine($"… (truncated at {_options.MaxFileChars} characters)");

        return sb.ToString().TrimEnd();
    }
}
