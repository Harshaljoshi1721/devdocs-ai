using System.Text;
using System.Text.Json;

namespace DevDocsAI.Application.Features.Agents;

/// <summary>Canonical tool names, shared by the tools and the agent catalog.</summary>
public static class ToolNames
{
    public const string SearchProject = "SearchProject";
    public const string ReadFile = "ReadFile";
    public const string GetProjectStructure = "GetProjectStructure";
}

/// <summary>
/// A capability an agent can invoke. <see cref="Description"/> documents the
/// arguments and is rendered into the system prompt. Execution returns the
/// observation text fed back to the model. Invalid arguments throw; the loop
/// records the failure and feeds the message back.
/// </summary>
public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    Task<string> ExecuteAsync(Guid projectId, JsonElement arguments, CancellationToken ct);
}

/// <summary>Helpers for reading tool arguments with clear failures.</summary>
public static class ToolArgs
{
    public static string RequireString(JsonElement args, string name)
    {
        if (args.ValueKind == JsonValueKind.Object &&
            args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
        {
            var s = v.GetString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        throw new InvalidOperationException($"Missing required string argument '{name}'.");
    }

    public static int? OptionalInt(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var v))
            return null;

        // Note: TryGetInt32 THROWS on a non-Number element, so guard by kind first.
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i))
            return i;

        // Tolerate models that stringify numbers, e.g. {"topK": "1"}.
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s))
            return s;

        return null;
    }
}

/// <summary>Resolves tools by name (honouring an agent's allow-list) and describes them for prompts.</summary>
public sealed class ToolRegistry(IEnumerable<IAgentTool> tools)
{
    private readonly Dictionary<string, IAgentTool> _byName =
        tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    public IAgentTool? Resolve(string name, IReadOnlyCollection<string> allowed) =>
        allowed.Contains(name, StringComparer.OrdinalIgnoreCase) && _byName.TryGetValue(name, out var t)
            ? t
            : null;

    public string Describe(IReadOnlyCollection<string> allowed)
    {
        var sb = new StringBuilder();
        foreach (var name in allowed)
        {
            if (_byName.TryGetValue(name, out var t))
                sb.AppendLine($"- {t.Name}: {t.Description}");
        }

        return sb.ToString().TrimEnd();
    }
}
