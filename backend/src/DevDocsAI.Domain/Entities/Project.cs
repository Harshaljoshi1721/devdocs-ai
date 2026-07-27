using DevDocsAI.Domain.Common;
using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// A codebase / knowledge space owned by a user. Ownership is recorded both as
/// <see cref="OwnerId"/> (the authorization source of truth for Phase 2) and as
/// an <see cref="ProjectRole.Owner"/> membership, so collaboration can be added later.
/// </summary>
public sealed class Project : Entity
{
    private readonly List<ProjectMember> _members = [];

    private Project() { } // EF

    private Project(string name, string? description, Guid ownerId)
    {
        Name = name;
        Description = description;
        OwnerId = ownerId;
    }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid OwnerId { get; private set; }

    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();

    /// <summary>Creates a project and seeds the owner's membership.</summary>
    public static Project Create(string name, string? description, Guid ownerId)
    {
        var project = new Project(name, description, ownerId);
        project._members.Add(new ProjectMember(project.Id, ownerId, ProjectRole.Owner));
        return project;
    }

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }
}
