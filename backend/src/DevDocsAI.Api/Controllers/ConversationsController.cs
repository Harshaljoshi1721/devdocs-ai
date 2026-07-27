using System.Text.Json;
using System.Text.Json.Serialization;
using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevDocsAI.Api.Controllers;

/// <summary>
/// Multi-turn chat scoped to a project. Conversations and their messages are
/// persisted; answers can be requested whole (<c>messages</c>) or streamed token
/// by token over Server-Sent Events (<c>messages/stream</c>).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/conversations")]
public sealed class ConversationsController(IChatService chat, ICurrentUser currentUser) : ControllerBase
{
    private static readonly JsonSerializerOptions StreamJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private Guid UserId => currentUser.UserId
        ?? throw new AuthenticationException("The request is not authenticated.");

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConversationResponse>>> List(
        Guid projectId, CancellationToken ct)
        => Ok(await chat.ListConversationsAsync(UserId, projectId, ct));

    [HttpPost]
    public async Task<ActionResult<ConversationResponse>> Create(
        Guid projectId, CreateConversationRequest request, CancellationToken ct)
    {
        var conversation = await chat.CreateConversationAsync(UserId, projectId, request, ct);
        return CreatedAtAction(nameof(Get), new { projectId, conversationId = conversation.Id }, conversation);
    }

    [HttpGet("{conversationId:guid}")]
    public async Task<ActionResult<ConversationDetail>> Get(
        Guid projectId, Guid conversationId, CancellationToken ct)
        => Ok(await chat.GetConversationAsync(UserId, projectId, conversationId, ct));

    [HttpDelete("{conversationId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid conversationId, CancellationToken ct)
    {
        await chat.DeleteConversationAsync(UserId, projectId, conversationId, ct);
        return NoContent();
    }

    /// <summary>Send a message and get the complete grounded answer in one response.</summary>
    [HttpPost("{conversationId:guid}/messages")]
    public async Task<ActionResult<MessageResponse>> Send(
        Guid projectId, Guid conversationId, SendMessageRequest request, CancellationToken ct)
        => Ok(await chat.SendMessageAsync(UserId, projectId, conversationId, request, ct));

    /// <summary>Send a message and stream the answer as SSE token deltas, ending with the saved message.</summary>
    [HttpPost("{conversationId:guid}/messages/stream")]
    public async Task Stream(
        Guid projectId, Guid conversationId, SendMessageRequest request, CancellationToken ct)
    {
        var events = chat
            .StreamMessageAsync(UserId, projectId, conversationId, request, ct)
            .GetAsyncEnumerator(ct);

        // Trigger the first step (auth + validation + retrieval) before committing to
        // an SSE response, so those failures still surface as normal ProblemDetails.
        bool hasEvent = await events.MoveNextAsync();

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // disable proxy buffering

        try
        {
            while (hasEvent)
            {
                await WriteEventAsync(events.Current, ct);
                hasEvent = await events.MoveNextAsync();
            }
        }
        finally
        {
            await events.DisposeAsync();
        }
    }

    private async Task WriteEventAsync(ChatStreamEvent evt, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(evt, StreamJson);
        await Response.WriteAsync($"event: {evt.Type}\ndata: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
