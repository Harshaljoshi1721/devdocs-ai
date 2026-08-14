using System.Security.Cryptography;
using DevDocsAI.Application.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Infrastructure.Storage;

/// <summary>
/// Stores files on local disk under a configured root. Storage keys are
/// generated (project id + UUID), never derived from the client file name, and
/// every read/delete is confined to the root — so a crafted name or key cannot
/// escape the storage directory (path-traversal safe).
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(Guid projectId, string extension, Stream content, CancellationToken ct)
    {
        var key = $"{projectId:N}/{Guid.CreateVersion7():N}{SanitizeExtension(extension)}";
        var fullPath = ResolveWithinRoot(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var sha = SHA256.Create();
        long size = 0;
        var buffer = new byte[81920];

        await using (var fileStream = File.Create(fullPath))
        {
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                sha.TransformBlock(buffer, 0, read, null, 0);
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                size += read;
            }
        }

        sha.TransformFinalBlock([], 0, 0);
        var hash = Convert.ToHexStringLower(sha.Hash!);
        return new StoredFile(key, size, hash);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        Stream stream = File.OpenRead(ResolveWithinRoot(storageKey));
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var fullPath = ResolveWithinRoot(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>Resolves a key to an absolute path and refuses anything outside the root.</summary>
    private string ResolveWithinRoot(string storageKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, storageKey));
        var rootWithSep = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSep, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resolved storage path escapes the storage root.");
        }

        return fullPath;
    }

    private static string SanitizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        // Keep a leading dot plus alphanumerics only; drop anything unusual.
        return extension.All(c => c == '.' || char.IsLetterOrDigit(c)) ? extension : string.Empty;
    }
}
