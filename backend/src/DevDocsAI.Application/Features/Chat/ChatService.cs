using System.Runtime.CompilerServices;
using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Rag;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using DevDocsAI.Domain.ValueObjects;

namespace DevDocsAI.Application.Features.Chat;

public interface IChatService
{
    Task<ConversationResponse> CreateConversationAsync(
        Guid userId, Guid projectId, CreateConversationRequest request, CancellationToken ct);

    Task<IReadOnlyList<ConversationResponse>> ListConversationsAsync(
        Guid userId, Guid projectId, CancellationToken ct);

    Task<ConversationDetail> GetConversationAsync(
        Guid userId, Guid projectId, Guid conversationId, CancellationToken ct);

    Task DeleteConversationAsync(
        Guid userId, Guid projectId, Guid conversationId, CancellationToken ct);

    Task<MessageResponse> SendMessageAsync(
        Guid userId, Guid projectId, Guid conversationId, SendMessageRequest request, CancellationToken ct);

    IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(
        Guid userId, Guid projectId, Guid conversationId, SendMessageRequest request, CancellationToken ct);
}

/// <summary>
/// Multi-turn, project-scoped chat. Each turn is grounded the same way as one-shot
/// ask — retrieve project chunks, answer only from them — but the persisted
/// conversation history is replayed to the model so follow-ups keep their context.
/// The user's question and the assistant's answer (with its citations) are persisted.
/// </summary>
public sealed class ChatService(
    IProjectRepository projects,
    IConversationRepository conversations,
    IRetrievalService retrieval,
    IChatCompletionService chat,
    IUnitOfWork uow) : IChatService
{
    private const string DefaultTitle = "New conversation";
    private const int TitleMaxLength = 80;

    public async Task<ConversationResponse> CreateConversationAsync(
        Guid userId, Guid projectId, CreateConversationRequest request, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);

        var title = string.IsNullOrWhiteSpace(request.Title) ? DefaultTitle : Truncate(request.Title.Trim());
        var conversation = Conversation.Start(projectId, userId, title);
        await conversations.AddAsync(conversation, ct);
        await uow.SaveChangesAsync(ct);

        return ToResponse(conversation);
    }

    public async Task<IReadOnlyList<ConversationResponse>> ListConversationsAsync(
        Guid userId, Guid projectId, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);
        var list = await conversations.ListByProjectAsync(projectId, userId, ct);
        return list.Select(ToResponse).ToList();
    }

    public async Task<ConversationDetail> GetConversationAsync(
        Guid userId, Guid projectId, Guid conversationId, CancellationToken ct)
    {
        var conversation = await LoadOwnedWithMessagesAsync(userId, projectId, conversationId, ct);
        return new ConversationDetail(
            conversation.Id, conversation.ProjectId, conversation.Title,
            conversation.CreatedAt, conversation.UpdatedAt, OrderedMessages(conversation));
    }

    public async Task DeleteConversationAsync(
        Guid userId, Guid projectId, Guid conversationId, CancellationToken ct)
    {
        var conversation = await conversations.GetByIdAsync(conversationId, ct);
        EnsureOwned(conversation, userId, projectId);
        conversations.Remove(conversation!);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<MessageResponse> SendMessageAsync(
        Guid userId, Guid projectId, Guid conversationId, SendMessageRequest request, CancellationToken ct)
    {
        var (conversation, history) = await BeginTurnAsync(userId, projectId, conversationId, request, ct);

        var hits = await retrieval.RetrieveAsync(projectId, request.Question, request.TopK, ct);
        string answer;
        if (hits.Count == 0)
        {
            answer = GroundedChat.NoContextAnswer;
        }
        else
        {
            var completion = await chat.CompleteAsync(BuildRequest(history, hits, request.Question), ct);
            answer = completion.Text;
        }

        var assistant = await CompleteTurnAsync(conversation, answer, hits, ct);
        return ToResponse(assistant);
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(
        Guid userId, Guid projectId, Guid conversationId, SendMessageRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var (conversation, history) = await BeginTurnAsync(userId, projectId, conversationId, request, ct);

        var hits = await retrieval.RetrieveAsync(projectId, request.Question, request.TopK, ct);

        string answer;
        if (hits.Count == 0)
        {
            answer = GroundedChat.NoContextAnswer;
            yield return ChatStreamEvent.Token(answer);
        }
        else
        {
            var buffer = new System.Text.StringBuilder();
            await foreach (var delta in chat.StreamAsync(BuildRequest(history, hits, request.Question), ct))
            {
                buffer.Append(delta);
                yield return ChatStreamEvent.Token(delta);
            }

            answer = buffer.ToString();
        }

        var assistant = await CompleteTurnAsync(conversation, answer, hits, ct);
        yield return ChatStreamEvent.Done(ToResponse(assistant));
    }

    /// <summary>
    /// Loads and authorizes the conversation, captures the prior history for the
    /// model, then persists the user's question so it survives even a failed answer.
    /// </summary>
    private async Task<(Conversation Conversation, IReadOnlyList<ChatMessage> History)> BeginTurnAsync(
        Guid userId, Guid projectId, Guid conversationId, SendMessageRequest request, CancellationToken ct)
    {
        var conversation = await LoadOwnedWithMessagesAsync(userId, projectId, conversationId, ct);
        ValidateQuestion(request.Question);
        var question = request.Question.Trim();

        var history = OrderedMessages(conversation)
            .Select(m => new ChatMessage(
                m.Role == nameof(MessageRole.Assistant) ? ChatRole.Assistant : ChatRole.User, m.Content))
            .ToList();

        if (conversation.Messages.Count == 0)
        {
            conversation.Rename(Truncate(question));
        }

        conversation.AddMessage(MessageRole.User, question);
        await uow.SaveChangesAsync(ct);

        return (conversation, history);
    }

    private async Task<Message> CompleteTurnAsync(
        Conversation conversation, string answer, IReadOnlyList<SearchHit> hits, CancellationToken ct)
    {
        var citations = hits
            .Select(h => new MessageCitation(h.DocumentId, h.DocumentName, h.Path, h.StartLine, h.EndLine))
            .ToList();

        var assistant = conversation.AddMessage(MessageRole.Assistant, answer, citations);
        await uow.SaveChangesAsync(ct);
        return assistant;
    }

    private static ChatRequest BuildRequest(
        IReadOnlyList<ChatMessage> history, IReadOnlyList<SearchHit> hits, string question)
    {
        var messages = new List<ChatMessage>(history)
        {
            new(ChatRole.User, GroundedChat.BuildUserTurn(hits, question)),
        };
        return new ChatRequest(GroundedChat.SystemPrompt, messages);
    }

    private async Task<Conversation> LoadOwnedWithMessagesAsync(
        Guid userId, Guid projectId, Guid conversationId, CancellationToken ct)
    {
        var conversation = await conversations.GetWithMessagesAsync(conversationId, ct);
        EnsureOwned(conversation, userId, projectId);
        return conversation!;
    }

    private static void EnsureOwned(Conversation? conversation, Guid userId, Guid projectId)
    {
        if (conversation is null || conversation.UserId != userId || conversation.ProjectId != projectId)
        {
            throw new NotFoundException("Conversation not found.");
        }
    }

    private async Task EnsureProjectOwnedAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null || project.OwnerId != userId)
        {
            throw new NotFoundException("Project not found.");
        }
    }

    private static void ValidateQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["question"] = ["A non-empty question is required."],
            });
        }
    }

    // OrderBy is a stable sort, so messages saved in the same instant keep their
    // insertion order; across turns each save stamps a distinct CreatedAt.
    private static IReadOnlyList<MessageResponse> OrderedMessages(Conversation conversation) =>
        conversation.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(ToResponse)
            .ToList();

    private static ConversationResponse ToResponse(Conversation c) =>
        new(c.Id, c.ProjectId, c.Title, c.CreatedAt, c.UpdatedAt);

    private static MessageResponse ToResponse(Message m) =>
        new(
            m.Id,
            m.Role.ToString(),
            m.Content,
            m.Citations
                .Select(c => new Citation(c.DocumentId, c.DocumentName, c.Path, c.StartLine, c.EndLine))
                .ToList(),
            m.CreatedAt);

    private static string Truncate(string text) =>
        text.Length <= TitleMaxLength ? text : text[..TitleMaxLength].TrimEnd() + "…";
}
