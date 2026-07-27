using DevDocsAI.Domain.Common;
using DevDocsAI.Domain.Enums;
using DevDocsAI.Domain.ValueObjects;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// A multi-turn chat session scoped to a project and owned by the user who
/// started it. It is the aggregate root for its <see cref="Message"/>s; messages
/// are only created through <see cref="AddMessage"/> so they stay consistent.
/// </summary>
public sealed class Conversation : Entity
{
    private readonly List<Message> _messages = [];

    private Conversation() { } // EF

    private Conversation(Guid projectId, Guid userId, string title)
    {
        ProjectId = projectId;
        UserId = userId;
        Title = title;
    }

    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;

    /// <summary>Messages in insertion (chronological) order.</summary>
    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();

    public static Conversation Start(Guid projectId, Guid userId, string title) =>
        new(projectId, userId, title);

    public Message AddMessage(
        MessageRole role, string content, IEnumerable<MessageCitation>? citations = null)
    {
        var message = new Message(Id, role, content, citations);
        _messages.Add(message);
        return message;
    }

    public void Rename(string title) => Title = title;
}
