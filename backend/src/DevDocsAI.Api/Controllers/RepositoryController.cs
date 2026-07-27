using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevDocsAI.Api.Controllers;

/// <summary>Connect a public GitHub repository to a project and ingest it (Phase 7).</summary>
[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/repository")]
public sealed class RepositoryController(IRepositoryConnectionService repositories, ICurrentUser currentUser)
    : ControllerBase
{
    private Guid UserId => currentUser.UserId
        ?? throw new AuthenticationException("The request is not authenticated.");

    [HttpPost]
    public async Task<ActionResult<RepositoryConnectionResponse>> Connect(
        Guid projectId, ConnectRepositoryRequest request, CancellationToken ct)
    {
        var connection = await repositories.ConnectAsync(UserId, projectId, request, ct);
        return AcceptedAtAction(nameof(Get), new { projectId }, connection);
    }

    [HttpGet]
    public async Task<ActionResult<RepositoryConnectionResponse>> Get(Guid projectId, CancellationToken ct)
    {
        var connection = await repositories.GetAsync(UserId, projectId, ct);
        return connection is null ? NotFound() : Ok(connection);
    }

    [HttpPost("resync")]
    public async Task<ActionResult<RepositoryConnectionResponse>> Resync(Guid projectId, CancellationToken ct)
        => Accepted(await repositories.ResyncAsync(UserId, projectId, ct));

    [HttpDelete]
    public async Task<IActionResult> Disconnect(Guid projectId, CancellationToken ct)
    {
        await repositories.DisconnectAsync(UserId, projectId, ct);
        return NoContent();
    }
}
