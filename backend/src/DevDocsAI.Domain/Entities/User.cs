using DevDocsAI.Domain.Common;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// An authenticated account. Local accounts store a password hash; the model
/// also tolerates a future external-identity identifier without schema change.
/// </summary>
public sealed class User : Entity
{
    private User() { } // EF

    public User(string email, string name, string passwordHash)
    {
        Email = email;
        Name = name;
        PasswordHash = passwordHash;
    }

    public string Email { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;

    public void Rename(string name) => Name = name;
}
