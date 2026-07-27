using DevDocsAI.Domain.Entities;

namespace DevDocsAI.Application.Abstractions.Security;

/// <summary>Hashes and verifies user passwords (salted, one-way).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public sealed record AccessToken(string Value, DateTime ExpiresAt);

public sealed record GeneratedRefreshToken(string RawValue, string Hash, DateTime ExpiresAt);

/// <summary>Issues JWT access tokens and opaque, hashable refresh tokens.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
    GeneratedRefreshToken CreateRefreshToken();

    /// <summary>Deterministic hash of a raw refresh token, for lookup/comparison.</summary>
    string HashRefreshToken(string rawValue);
}

/// <summary>The authenticated caller for the current request.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
}
