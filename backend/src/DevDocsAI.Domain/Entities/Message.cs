using DevDocsAI.Domain.Common;
using DevDocsAI.Domain.Enums;
using DevDocsAI.Domain.ValueObjects;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// A single turn in a <see cref="Conversation"/>. Assistant messages may carry
/// the citations that grounded them; user messages carry none. Created through
/// <see cref="Conversation.AddMessage"/> so it is always bound to a conversation.
/// </summary>
public sealed class Message : Entity
{
    private readonly List<MessageCitation> _citations = [];

    private Message() { } // EF

    internal Message(Guid conversationId, MessageRole role, string content, IEnumerable<MessageCitation>? citations)
    {
        ConversationId = conversationId;
        Role = role;
        Content = content;
        if (citations is not null)
        {
            _citations.AddRange(citations);
        }
    }

    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = null!;

    public IReadOnlyList<MessageCitation> Citations => _citations.AsReadOnly();
}
