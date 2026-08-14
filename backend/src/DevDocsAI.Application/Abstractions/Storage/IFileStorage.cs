namespace DevDocsAI.Application.Abstractions.Storage;

public sealed record StoredFile(string StorageKey, long SizeBytes, string ContentHash);

/// <summary>
/// Persists uploaded file bytes behind an opaque storage key. Implementations
/// generate the key themselves (never derived from client input), so a
/// malicious file name cannot escape the storage root.
/// </summary>
public interface IFileStorage
{
    /// <summary>Stores the content and returns its key, byte length, and SHA-256 hash.</summary>
    Task<StoredFile> SaveAsync(Guid projectId, string extension, Stream content, CancellationToken ct);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);

    Task DeleteAsync(string storageKey, CancellationToken ct);
}
