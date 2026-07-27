using DevDocsAI.Domain.Common;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// A persisted, revocable refresh token. Only a hash of the token value is
/// stored — the raw token is returned to the client once and never kept.
/// Rotation revokes the old token when a new one is issued.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken() { } // EF

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public bool IsActive(DateTime now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTime now) => RevokedAt ??= now;
}
