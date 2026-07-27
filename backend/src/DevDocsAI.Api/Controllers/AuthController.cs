using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevDocsAI.Api.Controllers;

public sealed record AuthTokenResponse(string AccessToken, DateTime ExpiresAt, UserDto User);

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    private const string RefreshCookieName = "refresh_token";
    private const string RefreshCookiePath = "/api/v1/auth";

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokenResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        return Respond(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokenResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        return Respond(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokenResponse>> Refresh(CancellationToken ct)
    {
        var raw = Request.Cookies[RefreshCookieName]
            ?? throw new AuthenticationException("Missing refresh token.");
        var result = await auth.RefreshAsync(raw, ct);
        return Respond(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var raw = Request.Cookies[RefreshCookieName];
        if (raw is not null)
        {
            await auth.LogoutAsync(raw, ct);
        }

        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });
        return NoContent();
    }

    private ActionResult<AuthTokenResponse> Respond(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookiePath,
            Expires = result.RefreshTokenExpiresAt,
        });

        return Ok(new AuthTokenResponse(result.AccessToken, result.AccessTokenExpiresAt, result.User));
    }
}
