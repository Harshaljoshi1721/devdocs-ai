using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Rag;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevDocsAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}")]
public sealed class RagController(IRagService rag, ICurrentUser currentUser) : ControllerBase
{
    private Guid UserId => currentUser.UserId
        ?? throw new AuthenticationException("The request is not authenticated.");

    /// <summary>Natural-language semantic search over the project's indexed chunks.</summary>
    [HttpPost("search")]
    public async Task<ActionResult<SearchResponse>> Search(
        Guid projectId, SearchRequest request, CancellationToken ct)
        => Ok(await rag.SearchAsync(UserId, projectId, request, ct));

    /// <summary>Ask a question and get a grounded, citation-backed answer.</summary>
    [HttpPost("ask")]
    public async Task<ActionResult<AskResponse>> Ask(
        Guid projectId, AskRequest request, CancellationToken ct)
        => Ok(await rag.AskAsync(UserId, projectId, request, ct));
}
