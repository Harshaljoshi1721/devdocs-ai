using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DevDocsAI.Application.Abstractions;
using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DevDocsAI.Infrastructure.Security;

/// <summary>
/// Issues HS256 JWT access tokens and high-entropy opaque refresh tokens.
/// Refresh tokens are returned raw to the caller once; only a SHA-256 hash is
/// persisted, so a database read cannot recover a usable token.
/// </summary>
public sealed class TokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(User user)
    {
        var expiresAt = clock.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = clock.UtcNow,
            NotBefore = clock.UtcNow,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.Name),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expiresAt);
    }

    public GeneratedRefreshToken CreateRefreshToken()
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var expiresAt = clock.UtcNow.AddDays(_options.RefreshTokenDays);
        return new GeneratedRefreshToken(raw, HashRefreshToken(raw), expiresAt);
    }

    public string HashRefreshToken(string rawValue)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return Convert.ToHexStringLower(bytes);
    }
}
