using DevDocsAI.Application.Features.Rag;

namespace DevDocsAI.Application.Features.Chat;

public sealed record CreateConversationRequest(string? Title);

public sealed record ConversationResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record MessageResponse(
    Guid Id,
    string Role,
    string Content,
    IReadOnlyList<Citation> Citations,
    DateTime CreatedAt);

/// <summary>A conversation together with its full, ordered message history.</summary>
public sealed record ConversationDetail(
    Guid Id,
    Guid ProjectId,
    string Title,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<MessageResponse> Messages);

public sealed record SendMessageRequest(string Question, int? TopK);

/// <summary>
/// One event in a streamed answer: incremental <c>token</c> deltas followed by a
/// single terminal <c>done</c> event carrying the persisted assistant message.
/// </summary>
public sealed record ChatStreamEvent(string Type, string? Text = null, MessageResponse? Message = null)
{
    public static ChatStreamEvent Token(string text) => new("token", Text: text);

    public static ChatStreamEvent Done(MessageResponse message) => new("done", Message: message);
}
