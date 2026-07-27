using DevDocsAI.Application.Abstractions;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Ingestion;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace DevDocsAI.Application.Features.Repositories;

public interface IRepositoryConnectionService
{
    Task<RepositoryConnectionResponse> ConnectAsync(Guid userId, Guid projectId, ConnectRepositoryRequest request, CancellationToken ct);
    Task<RepositoryConnectionResponse?> GetAsync(Guid userId, Guid projectId, CancellationToken ct);
    Task<RepositoryConnectionResponse> ResyncAsync(Guid userId, Guid projectId, CancellationToken ct);
    Task DisconnectAsync(Guid userId, Guid projectId, CancellationToken ct);
}

/// <summary>
/// Manages a project's single repository connection: validate + create (replacing
/// any existing), report status, re-sync, and disconnect. Ingestion runs in the
/// background via <see cref="IRepositoryIngestor"/>.
/// </summary>
public sealed class RepositoryConnectionService(
    IProjectRepository projects,
    IRepositoryConnectionRepository connections,
    IDocumentService documents,
    IBackgroundTaskQueue queue,
    IUnitOfWork uow) : IRepositoryConnectionService
{
    public async Task<RepositoryConnectionResponse> ConnectAsync(
        Guid userId, Guid projectId, ConnectRepositoryRequest request, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);

        if (!GitHubUrlParser.TryParse(request.Url, out var repo))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["url"] = ["Enter a valid public GitHub repository URL, e.g. https://github.com/owner/repo."],
            });
        }

        // Replace any existing connection for this project. Commit the deletion in
        // its own SaveChanges before inserting the new row — otherwise EF may order
        // the insert before the delete and violate the unique index on ProjectId.
        var existing = await connections.GetByProjectAsync(projectId, ct);
        if (existing is not null)
        {
            await documents.RemoveByConnectionAsync(existing.Id, ct);
            connections.Remove(existing);
            await uow.SaveChangesAsync(ct);
        }

        var @ref = string.IsNullOrWhiteSpace(request.Ref) ? repo.Ref : request.Ref.Trim();
        var connection = RepositoryConnection.Connect(
            projectId, RepositoryProvider.GitHub,
            $"https://github.com/{repo.Owner}/{repo.Repo}", repo.Owner, repo.Repo, @ref);

        await connections.AddAsync(connection, ct);
        await uow.SaveChangesAsync(ct);
        await EnqueueIngestionAsync(connection.Id, ct);

        return Map(connection);
    }

    public async Task<RepositoryConnectionResponse?> GetAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);
        var connection = await connections.GetByProjectAsync(projectId, ct);
        return connection is null ? null : Map(connection);
    }

    public async Task<RepositoryConnectionResponse> ResyncAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);
        var connection = await connections.GetByProjectAsync(projectId, ct)
            ?? throw new NotFoundException("No repository is connected to this project.");

        connection.Reset();
        await uow.SaveChangesAsync(ct);
        await EnqueueIngestionAsync(connection.Id, ct);
        return Map(connection);
    }

    public async Task DisconnectAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);
        var connection = await connections.GetByProjectAsync(projectId, ct);
        if (connection is null) return;

        await documents.RemoveByConnectionAsync(connection.Id, ct);
        connections.Remove(connection);
        await uow.SaveChangesAsync(ct);
    }

    private async Task EnqueueIngestionAsync(Guid connectionId, CancellationToken ct) =>
        await queue.EnqueueAsync(
            (sp, token) => new ValueTask(sp.GetRequiredService<IRepositoryIngestor>().IngestAsync(connectionId, token)),
            ct);

    private async Task EnsureProjectOwnedAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null || project.OwnerId != userId)
        {
            throw new NotFoundException("Project not found.");
        }
    }

    private static RepositoryConnectionResponse Map(RepositoryConnection c) => new(
        c.Id, c.ProjectId, c.Provider.ToString(), c.Url, c.Owner, c.Repo, c.Ref, c.CommitSha,
        c.Status.ToString(), c.Error, c.FileCount, c.CreatedAt, c.UpdatedAt);
}
