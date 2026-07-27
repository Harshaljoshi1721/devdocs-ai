using System.Formats.Tar;
using System.IO.Compression;
using DevDocsAI.Application.Abstractions;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Features.Ingestion;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Application.Features.Repositories;

/// <summary>Ingests a connected repository: download the tarball, walk it, feed supported files to the pipeline.</summary>
public interface IRepositoryIngestor
{
    Task IngestAsync(Guid connectionId, CancellationToken ct);
}

public sealed class RepositoryIngestor(
    IRepositoryConnectionRepository connections,
    IGitHubRepositoryClient github,
    IDocumentService documentService,
    IDocumentIngestor ingestor,
    IFileFilter fileFilter,
    IUnitOfWork uow,
    IOptions<RepoIngestionOptions> options) : IRepositoryIngestor
{
    private readonly RepoIngestionOptions _options = options.Value;

    public async Task IngestAsync(Guid connectionId, CancellationToken ct)
    {
        var connection = await connections.GetByIdAsync(connectionId, ct);
        if (connection is null) return; // deleted before the job ran

        // Idempotent: clear any documents from a previous run of this connection.
        await documentService.RemoveByConnectionAsync(connectionId, ct);

        connection.MarkProcessing();
        await uow.SaveChangesAsync(ct);

        try
        {
            await using var archive = await github.DownloadTarballAsync(
                connection.Owner, connection.Repo, connection.Ref, ct);

            var acceptedIds = new List<Guid>();
            var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0;

            await using var gzip = new GZipStream(archive.Content, CompressionMode.Decompress);
            await using var tar = new TarReader(gzip);

            while (await tar.GetNextEntryAsync(cancellationToken: ct) is { } entry)
            {
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                    continue;
                if (entry.DataStream is null) continue;

                var path = StripRoot(entry.Name);
                if (path is null) continue;                       // top-level dir entry / unsafe
                if (!fileFilter.IsAllowed(path)) continue;        // cheap pre-filter (allowlist + secret)

                var length = entry.Length;
                if (length <= 0 || length > _options.MaxFileBytes) continue;

                totalBytes += length;
                if (totalBytes > _options.MaxTotalBytes)
                    throw new InvalidOperationException(
                        $"Repository exceeds the {_options.MaxTotalBytes / (1024 * 1024)} MB ingestion limit.");

                if (acceptedIds.Count >= _options.MaxFiles)
                    throw new InvalidOperationException(
                        $"Repository exceeds the {_options.MaxFiles}-file ingestion limit.");

                using var buffer = new MemoryStream();
                await entry.DataStream.CopyToAsync(buffer, ct);
                buffer.Position = 0;

                var outcome = await ingestor.IngestAsync(
                    connection.ProjectId, path, length, buffer, connection.Id, seenHashes, ct);
                if (outcome.Document is { } doc) acceptedIds.Add(doc.Id);
            }

            await uow.SaveChangesAsync(ct);
            await ingestor.EnqueueProcessingAsync(acceptedIds, ct);

            connection.MarkCompleted(archive.CommitSha, acceptedIds.Count);
            await uow.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            connection.MarkFailed(ex.Message);
            await uow.SaveChangesAsync(ct);
        }
    }

    /// <summary>Removes the archive's top-level "{repo}-{sha}/" segment; rejects unsafe paths.</summary>
    private static string? StripRoot(string entryName)
    {
        var normalized = entryName.Replace('\\', '/');
        var slash = normalized.IndexOf('/');
        if (slash < 0 || slash == normalized.Length - 1) return null; // no file part

        var path = normalized[(slash + 1)..];
        if (path.Length == 0) return null;
        if (path.StartsWith('/') || path.Contains("..") || path.Contains(':')) return null;
        return path;
    }
}
