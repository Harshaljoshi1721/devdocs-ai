using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Usage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevDocsAI.Api.Controllers;

/// <summary>AI usage summary for a project (Phase 9).</summary>
[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/usage")]
public sealed class UsageController(IUsageService usage, ICurrentUser currentUser) : ControllerBase
{
    private Guid UserId => currentUser.UserId
        ?? throw new AuthenticationException("The request is not authenticated.");

    [HttpGet]
    public async Task<ActionResult<UsageSummaryResponse>> Get(Guid projectId, CancellationToken ct)
        => Ok(await usage.SummarizeAsync(UserId, projectId, ct));
}
