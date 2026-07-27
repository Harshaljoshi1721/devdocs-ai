using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Domain.Entities;

namespace DevDocsAI.Application.Features.Rag;

public interface IRagService
{
    Task<SearchResponse> SearchAsync(Guid userId, Guid projectId, SearchRequest request, CancellationToken ct);
    Task<AskResponse> AskAsync(Guid userId, Guid projectId, AskRequest request, CancellationToken ct);
}

/// <summary>
/// One-shot retrieval-augmented generation over a project's indexed chunks:
/// return the ranked sources (search) or ground an LLM answer in them with
/// citations (ask). Retrieval and grounding are shared with multi-turn chat via
/// <see cref="IRetrievalService"/> and <see cref="GroundedChat"/>.
/// </summary>
public sealed class RagService(
    IProjectRepository projects,
    IRetrievalService retrieval,
    IChatCompletionService chat) : IRagService
{
    public async Task<SearchResponse> SearchAsync(
        Guid userId, Guid projectId, SearchRequest request, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);
        ValidateQuery(request.Query);

        var hits = await retrieval.RetrieveAsync(projectId, request.Query, request.TopK, ct);
        return new SearchResponse(request.Query, hits);
    }

    public async Task<AskResponse> AskAsync(
        Guid userId, Guid projectId, AskRequest request, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);
        ValidateQuery(request.Question);

        var hits = await retrieval.RetrieveAsync(projectId, request.Question, request.TopK, ct);
        if (hits.Count == 0)
        {
            return new AskResponse(GroundedChat.NoContextAnswer, [], Grounded: false);
        }

        var userMessage = GroundedChat.BuildUserTurn(hits, request.Question);
        var completion = await chat.CompleteAsync(
            new ChatRequest(GroundedChat.SystemPrompt, [new ChatMessage(ChatRole.User, userMessage)]), ct);

        var citations = hits
            .Select(h => new Citation(h.DocumentId, h.DocumentName, h.Path, h.StartLine, h.EndLine))
            .ToList();

        return new AskResponse(completion.Text, citations, Grounded: true);
    }

    private static void ValidateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["query"] = ["A non-empty query is required."],
            });
        }
    }

    private async Task EnsureProjectOwnedAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        Project? project = await projects.GetByIdAsync(projectId, ct);
        if (project is null || project.OwnerId != userId)
        {
            throw new NotFoundException("Project not found.");
        }
    }
}
