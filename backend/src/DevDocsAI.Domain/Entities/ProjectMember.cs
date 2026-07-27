using DevDocsAI.Domain.Common;
using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Domain.Entities;

/// <summary>Associates a user with a project under a given role.</summary>
public sealed class ProjectMember : Entity
{
    private ProjectMember() { } // EF

    public ProjectMember(Guid projectId, Guid userId, ProjectRole role)
    {
        ProjectId = projectId;
        UserId = userId;
        Role = role;
    }

    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectRole Role { get; private set; }
}
