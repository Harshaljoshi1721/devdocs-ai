namespace DevDocsAI.Application.Abstractions.AI;

public enum ChatRole
{
    User,
    Assistant,
}

public sealed record ChatMessage(ChatRole Role, string Content);

public sealed record ChatRequest(string SystemPrompt, IReadOnlyList<ChatMessage> Messages);

public sealed record ChatCompletion(string Text);

/// <summary>Generates a chat completion from a system prompt + messages. Provider-swappable.</summary>
public interface IChatCompletionService
{
    Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken ct);

    /// <summary>Streams the assistant's answer as incremental text deltas as the model produces them.</summary>
    IAsyncEnumerable<string> StreamAsync(ChatRequest request, CancellationToken ct);
}
