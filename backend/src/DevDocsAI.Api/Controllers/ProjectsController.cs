using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevDocsAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/projects")]
public sealed class ProjectsController(IProjectService projects, ICurrentUser currentUser) : ControllerBase
{
    private Guid UserId => currentUser.UserId
        ?? throw new AuthenticationException("The request is not authenticated.");

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> List(CancellationToken ct)
        => Ok(await projects.ListAsync(UserId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> Get(Guid id, CancellationToken ct)
        => Ok(await projects.GetAsync(UserId, id, ct));

    [HttpPost]
    public async Task<ActionResult<ProjectResponse>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var created = await projects.CreateAsync(UserId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> Update(Guid id, UpdateProjectRequest request, CancellationToken ct)
        => Ok(await projects.UpdateAsync(UserId, id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await projects.DeleteAsync(UserId, id, ct);
        return NoContent();
    }
}
