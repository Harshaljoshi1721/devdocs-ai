using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocsAI.Infrastructure.Persistence.Repositories;

public sealed class ConversationRepository(AppDbContext db) : IConversationRepository
{
    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Conversations.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Conversation?> GetWithMessagesAsync(Guid id, CancellationToken ct) =>
        db.Conversations
            .Include(c => c.Messages)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Conversation>> ListByProjectAsync(
        Guid projectId, Guid userId, CancellationToken ct) =>
        await db.Conversations
            .Where(c => c.ProjectId == projectId && c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Conversation conversation, CancellationToken ct) =>
        await db.Conversations.AddAsync(conversation, ct);

    public void Remove(Conversation conversation) => db.Conversations.Remove(conversation);
}
