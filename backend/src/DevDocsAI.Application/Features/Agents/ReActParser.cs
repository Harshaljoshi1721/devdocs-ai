using System.Text.Json;

namespace DevDocsAI.Application.Features.Agents;

/// <summary>One step the model produced in the ReAct loop.</summary>
public abstract record AgentStep;
public sealed record FinalStep(string Answer, string? Thought) : AgentStep;
public sealed record ActionStep(string Tool, JsonElement Arguments, string? Thought) : AgentStep;
public sealed record UnparseableStep(string Raw) : AgentStep;

/// <summary>
/// Parses a model turn into a ReAct step. Tolerates code fences and surrounding
/// prose by extracting the first balanced JSON object, then reading either a
/// <c>final_answer</c> or an <c>action</c> with a tool name.
/// </summary>
public static class ReActParser
{
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    public static AgentStep Parse(string text)
    {
        if (!TryExtractJson(text, out var json))
            return new UnparseableStep(text);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return new UnparseableStep(text); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new UnparseableStep(text);

            var thought = root.TryGetProperty("thought", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;

            if (root.TryGetProperty("final_answer", out var fa) && fa.ValueKind == JsonValueKind.String)
                return new FinalStep(fa.GetString() ?? string.Empty, thought);

            if (root.TryGetProperty("action", out var action) && action.ValueKind == JsonValueKind.Object &&
                action.TryGetProperty("tool", out var tool) && tool.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(tool.GetString()))
            {
                var arguments = action.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.Object
                    ? args.Clone()
                    : EmptyObject;
                return new ActionStep(tool.GetString()!, arguments, thought);
            }

            return new UnparseableStep(text);
        }
    }

    /// <summary>Extracts the substring from the first '{' to its matching '}'.</summary>
    private static bool TryExtractJson(string text, out string json)
    {
        json = string.Empty;
        var start = text.IndexOf('{');
        if (start < 0) return false;

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0) { json = text[start..(i + 1)]; return true; }
            }
        }

        return false;
    }
}
