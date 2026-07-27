using DevDocsAI.Domain.Entities;

namespace DevDocsAI.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
}

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Project>> ListByOwnerAsync(Guid ownerId, CancellationToken ct);
    Task AddAsync(Project project, CancellationToken ct);
    void Remove(Project project);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct);
    Task AddAsync(RefreshToken token, CancellationToken ct);
}

public interface IConversationRepository
{
    /// <summary>Loads a conversation without its messages (for ownership checks / delete).</summary>
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Loads a conversation with its messages (and their citations) in chronological order.</summary>
    Task<Conversation?> GetWithMessagesAsync(Guid id, CancellationToken ct);

    /// <summary>Lists a user's conversations in a project, most recently updated first (no messages).</summary>
    Task<IReadOnlyList<Conversation>> ListByProjectAsync(Guid projectId, Guid userId, CancellationToken ct);

    Task AddAsync(Conversation conversation, CancellationToken ct);
    void Remove(Conversation conversation);
}

/// <summary>Commits changes tracked across repositories in a single transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
