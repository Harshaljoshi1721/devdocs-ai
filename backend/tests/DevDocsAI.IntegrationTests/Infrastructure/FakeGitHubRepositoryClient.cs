using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using DevDocsAI.Application.Abstractions;

namespace DevDocsAI.IntegrationTests.Infrastructure;

/// <summary>Serves a fixed in-memory repository tarball — no network. Mirrors GitHub's "{repo}-{sha}/" layout.</summary>
public sealed class FakeGitHubRepositoryClient : IGitHubRepositoryClient
{
    public const string CommitSha = "0123456789abcdef0123456789abcdef01234567";

    public static readonly (string Path, string Content)[] Files =
    [
        ("src/auth.cs", "public class AuthController { /* JWT login */ }"),
        ("docs/architecture.md", "# Architecture\nThe gateway routes requests to services."),
        (".env", "SECRET=should-be-skipped"),        // secret → skipped
        ("assets/logo.png", "not really an image"),  // unsupported → skipped
    ];

    public Task<RepositoryArchive> DownloadTarballAsync(
        string owner, string repo, string? @ref, CancellationToken ct)
    {
        var raw = new MemoryStream();
        using (var gz = new GZipStream(raw, CompressionMode.Compress, leaveOpen: true))
        using (var tar = new TarWriter(gz, leaveOpen: true))
        {
            foreach (var (path, content) in Files)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, $"{repo}-{CommitSha}/{path}")
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
                };
                tar.WriteEntry(entry);
            }
        }

        raw.Position = 0;
        return Task.FromResult(new RepositoryArchive(CommitSha, raw));
    }
}
