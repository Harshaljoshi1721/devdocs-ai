using System.Text;

namespace DevDocsAI.Application.Features.Rag;

/// <summary>
/// Shared grounding pieces for answer generation: the system prompt that keeps
/// the model inside the retrieved context, the context builder, and the fallback
/// used when nothing relevant was found. Used by both one-shot ask and chat.
/// </summary>
internal static class GroundedChat
{
    public const string SystemPrompt =
        """
        You are DevDocs AI, a codebase assistant. Answer the user's question using ONLY the
        provided context, which consists of excerpts from their project's files.

        Rules:
        - Base your answer strictly on the context. Do not invent files, functions, or behavior.
        - If the context does not contain enough information, say clearly that it could not be
          determined from the available project context.
        - Refer to the relevant files by path when helpful.
        - Be concise and technical.
        """;

    public const string NoContextAnswer =
        "I couldn't find anything relevant in this project's indexed content to answer that. " +
        "Make sure the relevant files have been uploaded and processed.";

    public static string BuildContext(IReadOnlyList<SearchHit> hits)
    {
        var sb = new StringBuilder();
        var index = 1;
        foreach (var hit in hits)
        {
            sb.AppendLine($"[Source {index}] {hit.Path} (lines {hit.StartLine}-{hit.EndLine})");
            sb.AppendLine(hit.Snippet);
            sb.AppendLine();
            index++;
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Frames the retrieved context and the user's question into a single grounded turn.</summary>
    public static string BuildUserTurn(IReadOnlyList<SearchHit> hits, string question) =>
        $"Context from the project:\n\n{BuildContext(hits)}\n\nQuestion: {question}";
}
