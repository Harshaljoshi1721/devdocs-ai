using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Ingestion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevDocsAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/documents")]
public sealed class DocumentsController(IDocumentService documents, ICurrentUser currentUser) : ControllerBase
{
    private Guid UserId => currentUser.UserId
        ?? throw new AuthenticationException("The request is not authenticated.");

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentResponse>>> List(Guid projectId, CancellationToken ct)
        => Ok(await documents.ListAsync(UserId, projectId, ct));

    [HttpPost]
    [RequestSizeLimit(256 * 1024 * 1024)]
    public async Task<ActionResult<UploadResult>> Upload(
        Guid projectId,
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        var inputs = files
            .Select(f => new UploadFileInput(f.FileName, f.Length, f.OpenReadStream()))
            .ToList();

        var result = await documents.UploadAsync(UserId, projectId, inputs, ct);
        return Ok(result);
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid documentId, CancellationToken ct)
    {
        await documents.DeleteAsync(UserId, projectId, documentId, ct);
        return NoContent();
    }
}
