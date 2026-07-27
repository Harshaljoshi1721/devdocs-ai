namespace DevDocsAI.Application.Features.Projects;

public sealed record CreateProjectRequest(string Name, string? Description);

public sealed record UpdateProjectRequest(string Name, string? Description);

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
