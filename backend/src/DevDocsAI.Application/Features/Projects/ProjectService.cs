using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Common.Validation;
using DevDocsAI.Domain.Entities;
using FluentValidation;

namespace DevDocsAI.Application.Features.Projects;

public interface IProjectService
{
    Task<ProjectResponse> CreateAsync(Guid userId, CreateProjectRequest request, CancellationToken ct);
    Task<IReadOnlyList<ProjectResponse>> ListAsync(Guid userId, CancellationToken ct);
    Task<ProjectResponse> GetAsync(Guid userId, Guid projectId, CancellationToken ct);
    Task<ProjectResponse> UpdateAsync(Guid userId, Guid projectId, UpdateProjectRequest request, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid projectId, CancellationToken ct);
}

public sealed class ProjectService(
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    IValidator<CreateProjectRequest> createValidator,
    IValidator<UpdateProjectRequest> updateValidator) : IProjectService
{
    public async Task<ProjectResponse> CreateAsync(Guid userId, CreateProjectRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);

        var project = Project.Create(request.Name.Trim(), request.Description?.Trim(), userId);
        await projects.AddAsync(project, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(project);
    }

    public async Task<IReadOnlyList<ProjectResponse>> ListAsync(Guid userId, CancellationToken ct)
    {
        var owned = await projects.ListByOwnerAsync(userId, ct);
        return owned.Select(Map).ToList();
    }

    public async Task<ProjectResponse> GetAsync(Guid userId, Guid projectId, CancellationToken ct)
        => Map(await GetOwnedAsync(userId, projectId, ct));

    public async Task<ProjectResponse> UpdateAsync(Guid userId, Guid projectId, UpdateProjectRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);

        var project = await GetOwnedAsync(userId, projectId, ct);
        project.Update(request.Name.Trim(), request.Description?.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        return Map(project);
    }

    public async Task DeleteAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var project = await GetOwnedAsync(userId, projectId, ct);
        projects.Remove(project);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Loads a project the caller owns. A project that does not exist — or that
    /// belongs to another user — is reported as Not Found, denying access
    /// without revealing whether it exists (no cross-tenant enumeration).
    /// </summary>
    private async Task<Project> GetOwnedAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null || project.OwnerId != userId)
        {
            throw new NotFoundException("Project not found.");
        }

        return project;
    }

    private static ProjectResponse Map(Project p) =>
        new(p.Id, p.Name, p.Description, p.OwnerId, p.CreatedAt, p.UpdatedAt);
}
