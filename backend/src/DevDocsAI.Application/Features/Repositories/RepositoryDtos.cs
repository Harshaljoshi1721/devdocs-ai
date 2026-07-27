namespace DevDocsAI.Application.Features.Repositories;

public sealed record ConnectRepositoryRequest(string Url, string? Ref);

public sealed record RepositoryConnectionResponse(
    Guid Id,
    Guid ProjectId,
    string Provider,
    string Url,
    string Owner,
    string Repo,
    string? Ref,
    string? CommitSha,
    string Status,
    string? Error,
    int FileCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);
