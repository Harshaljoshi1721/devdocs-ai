using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Chat;
using DevDocsAI.Application.Features.Rag;
using DevDocsAI.Application.Features.Usage;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class ChatServiceTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IRetrievalService _retrieval = Substitute.For<IRetrievalService>();
    private readonly IChatCompletionService _chat = Substitute.For<IChatCompletionService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IUsageRecorder _usage = Substitute.For<IUsageRecorder>();
    private readonly ChatService _sut;

    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly Guid _projectId = Guid.CreateVersion7();
    private readonly SearchHit _hit;

    public ChatServiceTests()
    {
        _sut = new ChatService(_projects, _conversations, _retrieval, _chat, _uow, _usage);

        _hit = new SearchHit(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "auth.cs", "src/auth.cs",
            10, 25, 0.9, "public class AuthController {}");

        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, _userId));
    }

    private Conversation GivenConversation(string title = "Chat")
    {
        var conversation = Conversation.Start(_projectId, _userId, title);
        _conversations.GetWithMessagesAsync(conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);
        _conversations.GetByIdAsync(conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);
        return conversation;
    }

    private void GivenOneHit() =>
        _retrieval.RetrieveAsync(_projectId, Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SearchHit> { _hit });

    [Fact]
    public async Task CreateConversation_defaults_title_and_persists_for_the_owner()
    {
        var response = await _sut.CreateConversationAsync(
            _userId, _projectId, new CreateConversationRequest(null), default);

        response.Title.ShouldNotBeNullOrWhiteSpace();
        response.ProjectId.ShouldBe(_projectId);
        await _conversations.Received(1).AddAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateConversation_on_another_users_project_is_not_found()
    {
        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, Guid.CreateVersion7()));

        await Should.ThrowAsync<NotFoundException>(() => _sut.CreateConversationAsync(
            _userId, _projectId, new CreateConversationRequest("x"), default));
    }

    [Fact]
    public async Task SendMessage_with_context_persists_question_and_grounded_answer()
    {
        var conversation = GivenConversation();
        GivenOneHit();
        _chat.CompleteAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatCompletion("Authentication uses JWT tokens."));

        var response = await _sut.SendMessageAsync(
            _userId, _projectId, conversation.Id, new SendMessageRequest("How does auth work?", null), default);

        response.Role.ShouldBe(nameof(MessageRole.Assistant));
        response.Content.ShouldBe("Authentication uses JWT tokens.");
        response.Citations.ShouldHaveSingleItem().Path.ShouldBe("src/auth.cs");

        conversation.Messages.Count.ShouldBe(2);
        conversation.Messages[0].Role.ShouldBe(MessageRole.User);
        conversation.Messages[0].Content.ShouldBe("How does auth work?");
        conversation.Messages[1].Role.ShouldBe(MessageRole.Assistant);
        await _uow.ReceivedWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task SendMessage_with_no_context_returns_fallback_without_calling_the_model()
    {
        var conversation = GivenConversation();
        _retrieval.RetrieveAsync(_projectId, Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SearchHit>());

        var response = await _sut.SendMessageAsync(
            _userId, _projectId, conversation.Id, new SendMessageRequest("anything", null), default);

        response.Citations.ShouldBeEmpty();
        conversation.Messages[1].Content.ShouldContain("couldn't find");
        await _chat.DidNotReceive().CompleteAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessage_includes_prior_messages_as_history()
    {
        var conversation = GivenConversation();
        conversation.AddMessage(MessageRole.User, "First question?");
        conversation.AddMessage(MessageRole.Assistant, "First answer.");
        GivenOneHit();

        ChatRequest? captured = null;
        _chat.CompleteAsync(Arg.Do<ChatRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new ChatCompletion("Second answer."));

        await _sut.SendMessageAsync(
            _userId, _projectId, conversation.Id, new SendMessageRequest("Second question?", null), default);

        captured.ShouldNotBeNull();
        // history (2) + the new grounded turn (1)
        captured!.Messages.Count.ShouldBe(3);
        captured.Messages[0].Content.ShouldBe("First question?");
        captured.Messages[1].Content.ShouldBe("First answer.");
        captured.Messages[2].Content.ShouldContain("Second question?");
    }

    [Fact]
    public async Task SendMessage_names_the_conversation_from_the_first_question()
    {
        var conversation = GivenConversation("New conversation");
        GivenOneHit();
        _chat.CompleteAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatCompletion("answer"));

        await _sut.SendMessageAsync(
            _userId, _projectId, conversation.Id, new SendMessageRequest("How does auth work?", null), default);

        conversation.Title.ShouldBe("How does auth work?");
    }

    [Fact]
    public async Task SendMessage_on_a_conversation_owned_by_another_user_is_not_found()
    {
        var conversation = Conversation.Start(_projectId, Guid.CreateVersion7(), "Chat"); // different owner
        _conversations.GetWithMessagesAsync(conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);

        await Should.ThrowAsync<NotFoundException>(() => _sut.SendMessageAsync(
            _userId, _projectId, conversation.Id, new SendMessageRequest("q", null), default));
    }

    [Fact]
    public async Task SendMessage_with_empty_question_is_a_validation_error()
    {
        var conversation = GivenConversation();

        await Should.ThrowAsync<ValidationException>(() => _sut.SendMessageAsync(
            _userId, _projectId, conversation.Id, new SendMessageRequest("   ", null), default));
    }

    [Fact]
    public async Task GetConversation_returns_messages_in_chronological_order()
    {
        var conversation = GivenConversation();
        conversation.AddMessage(MessageRole.User, "Q");
        conversation.AddMessage(MessageRole.Assistant, "A");

        var detail = await _sut.GetConversationAsync(_userId, _projectId, conversation.Id, default);

        detail.Messages.Count.ShouldBe(2);
        detail.Messages[0].Content.ShouldBe("Q");
        detail.Messages[1].Content.ShouldBe("A");
    }

    [Fact]
    public async Task StreamMessage_emits_token_events_then_a_done_event_with_the_persisted_message()
    {
        var conversation = GivenConversation();
        GivenOneHit();
        _chat.StreamAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(ToAsync("JWT ", "tokens."));

        var events = new List<ChatStreamEvent>();
        await foreach (var e in _sut.StreamMessageAsync(
            _userId, _projectId, conversation.Id, new SendMessageRequest("How does auth work?", null), default))
        {
            events.Add(e);
        }

        events.Where(e => e.Type == "token").Select(e => e.Text).ShouldBe(["JWT ", "tokens."]);
        var done = events.Single(e => e.Type == "done");
        done.Message!.Content.ShouldBe("JWT tokens.");
        done.Message.Citations.ShouldHaveSingleItem().Path.ShouldBe("src/auth.cs");
        conversation.Messages[1].Content.ShouldBe("JWT tokens.");
    }

    private static async IAsyncEnumerable<string> ToAsync(params string[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }
}
