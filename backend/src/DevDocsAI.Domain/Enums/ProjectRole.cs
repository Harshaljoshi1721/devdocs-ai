namespace DevDocsAI.Domain.Enums;

/// <summary>
/// A user's role within a project. Phase 2 ships owner-only access; additional
/// roles/collaboration are enabled in a later phase.
/// </summary>
public enum ProjectRole
{
    Owner = 0,
    Member = 1,
}
