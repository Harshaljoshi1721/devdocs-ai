using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevDocsAI.Api.Controllers;

/// <summary>Run the built-in AI agents over a project and review their tool traces (Phase 8).</summary>
[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/agents")]
public sealed class AgentsController(IAgentService agents, ICurrentUser currentUser) : ControllerBase
{
    private Guid UserId => currentUser.UserId
        ?? throw new AuthenticationException("The request is not authenticated.");

    [HttpGet]
    public ActionResult<IReadOnlyList<AgentInfo>> List() => Ok(agents.ListAgents());

    [HttpPost("{agentType}/run")]
    public async Task<ActionResult<AgentRunResponse>> Run(
        Guid projectId, string agentType, AgentRunRequest request, CancellationToken ct)
        => Ok(await agents.RunAsync(UserId, projectId, agentType, request, ct));

    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<AgentRunSummary>>> Runs(Guid projectId, CancellationToken ct)
        => Ok(await agents.ListRunsAsync(UserId, projectId, ct));

    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<AgentRunResponse>> GetRun(Guid projectId, Guid runId, CancellationToken ct)
        => Ok(await agents.GetRunAsync(UserId, projectId, runId, ct));
}
