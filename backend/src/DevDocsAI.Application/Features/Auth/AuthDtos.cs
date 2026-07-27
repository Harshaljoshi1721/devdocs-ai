namespace DevDocsAI.Application.Features.Auth;

public sealed record RegisterRequest(string Email, string Name, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record UserDto(Guid Id, string Email, string Name);

/// <summary>
/// Result of an authentication use case. The controller returns the access
/// token + user in the response body and sets the raw refresh token as an
/// httpOnly cookie — the raw refresh value never appears in a JSON body.
/// </summary>
public sealed record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserDto User);
