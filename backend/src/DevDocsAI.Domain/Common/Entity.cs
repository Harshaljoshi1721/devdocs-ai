namespace DevDocsAI.Domain.Common;

/// <summary>
/// Base type for persistent entities. Ids are sortable (UUID v7). Audit
/// timestamps are maintained centrally by the persistence layer.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
