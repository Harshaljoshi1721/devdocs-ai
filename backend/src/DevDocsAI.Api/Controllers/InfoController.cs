using DevDocsAI.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Api.Controllers;

/// <summary>
/// Minimal public metadata endpoint. Establishes the versioned controller
/// convention (/api/v1/...) that feature controllers will follow in later phases.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public sealed class InfoController(IOptions<AppInfoOptions> appInfo) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        name = appInfo.Value.Name,
        version = appInfo.Value.Version,
        status = "ok",
    });
}
