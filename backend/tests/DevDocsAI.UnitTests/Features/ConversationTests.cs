using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using DevDocsAI.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class ConversationTests
{
    private readonly Guid _projectId = Guid.CreateVersion7();
    private readonly Guid _userId = Guid.CreateVersion7();

    [Fact]
    public void Start_creates_an_empty_conversation_owned_by_the_user()
    {
        var conversation = Conversation.Start(_projectId, _userId, "How does auth work?");

        conversation.ProjectId.ShouldBe(_projectId);
        conversation.UserId.ShouldBe(_userId);
        conversation.Title.ShouldBe("How does auth work?");
        conversation.Messages.ShouldBeEmpty();
    }

    [Fact]
    public void AddMessage_appends_a_message_bound_to_the_conversation()
    {
        var conversation = Conversation.Start(_projectId, _userId, "Chat");

        var message = conversation.AddMessage(MessageRole.User, "How does auth work?");

        message.ConversationId.ShouldBe(conversation.Id);
        message.Role.ShouldBe(MessageRole.User);
        message.Content.ShouldBe("How does auth work?");
        message.Citations.ShouldBeEmpty();
        conversation.Messages.ShouldHaveSingleItem().ShouldBe(message);
    }

    [Fact]
    public void AddMessage_preserves_insertion_order_and_carries_citations()
    {
        var conversation = Conversation.Start(_projectId, _userId, "Chat");
        var citation = new MessageCitation(Guid.CreateVersion7(), "auth.cs", "src/auth.cs", 10, 25);

        conversation.AddMessage(MessageRole.User, "How does auth work?");
        var answer = conversation.AddMessage(MessageRole.Assistant, "It uses JWT.", [citation]);

        conversation.Messages.Count.ShouldBe(2);
        conversation.Messages[0].Role.ShouldBe(MessageRole.User);
        conversation.Messages[1].ShouldBe(answer);
        answer.Citations.ShouldHaveSingleItem().Path.ShouldBe("src/auth.cs");
    }

    [Fact]
    public void Rename_updates_the_title()
    {
        var conversation = Conversation.Start(_projectId, _userId, "Untitled");

        conversation.Rename("Authentication questions");

        conversation.Title.ShouldBe("Authentication questions");
    }
}
