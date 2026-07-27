using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using DevDocsAI.Application.Abstractions;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Features.Ingestion;
using DevDocsAI.Application.Features.Repositories;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class RepositoryIngestorTests
{
    private readonly IRepositoryConnectionRepository _connections = Substitute.For<IRepositoryConnectionRepository>();
    private readonly IGitHubRepositoryClient _github = Substitute.For<IGitHubRepositoryClient>();
    private readonly IDocumentService _documentService = Substitute.For<IDocumentService>();
    private readonly IDocumentIngestor _ingestor = Substitute.For<IDocumentIngestor>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly RepositoryIngestor _sut;

    private readonly Guid _projectId = Guid.CreateVersion7();
    private readonly RepositoryConnection _connection;

    public RepositoryIngestorTests()
    {
        _sut = new RepositoryIngestor(
            _connections, _github, _documentService, _ingestor, new ExtensionFileFilter(), _uow,
            Options.Create(new RepoIngestionOptions()));

        _connection = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/octo/cat", "octo", "cat", null);
        _connections.GetByIdAsync(_connection.Id, Arg.Any<CancellationToken>()).Returns(_connection);

        // Accept anything the ingestor is asked to ingest, returning a fresh document.
        _ingestor.IngestAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<Stream>(),
                Arg.Any<Guid?>(), Arg.Any<ISet<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => IngestOutcome.Accepted(
                new Document(_projectId, call.ArgAt<string>(1), call.ArgAt<string>(1), FileType.Code, "h", 1, "k")));
    }

    private void GivenArchive(string commitSha, params (string Path, string Content)[] entries)
    {
        var tarGz = BuildTarGz($"cat-{commitSha}", entries);
        _github.DownloadTarballAsync("octo", "cat", null, Arg.Any<CancellationToken>())
            .Returns(new RepositoryArchive(commitSha, tarGz));
    }

    [Fact]
    public async Task Ingests_supported_files_and_completes_with_commit_and_count()
    {
        GivenArchive("sha1",
            ("src/app.cs", "class App {}"),
            ("README.md", "# hi"),
            ("logo.png", "binarybytes"),          // unsupported → skipped
            (".env", "SECRET=1"));                // secret → skipped

        await _sut.IngestAsync(_connection.Id, default);

        _connection.Status.ShouldBe(ProcessingStatus.Completed);
        _connection.CommitSha.ShouldBe("sha1");
        _connection.FileCount.ShouldBe(2);

        await _ingestor.Received(1).IngestAsync(
            _projectId, "src/app.cs", Arg.Any<long>(), Arg.Any<Stream>(),
            _connection.Id, Arg.Any<ISet<string>>(), Arg.Any<CancellationToken>());
        await _ingestor.Received(1).IngestAsync(
            _projectId, "README.md", Arg.Any<long>(), Arg.Any<Stream>(),
            _connection.Id, Arg.Any<ISet<string>>(), Arg.Any<CancellationToken>());
        await _ingestor.DidNotReceive().IngestAsync(
            _projectId, "logo.png", Arg.Any<long>(), Arg.Any<Stream>(),
            Arg.Any<Guid?>(), Arg.Any<ISet<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Removes_prior_repo_documents_before_ingesting()
    {
        GivenArchive("sha2", ("a.cs", "x"));

        await _sut.IngestAsync(_connection.Id, default);

        await _documentService.Received(1).RemoveByConnectionAsync(_connection.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Aborts_and_marks_failed_when_total_bytes_exceed_the_cap()
    {
        var big = new string('x', 2_000);
        var ingestor = new RepositoryIngestor(
            _connections, _github, _documentService, _ingestor, new ExtensionFileFilter(), _uow,
            Options.Create(new RepoIngestionOptions { MaxTotalBytes = 1_000 }));
        GivenArchive("sha3", ("a.cs", big), ("b.cs", big));

        await ingestor.IngestAsync(_connection.Id, default);

        _connection.Status.ShouldBe(ProcessingStatus.Failed);
        _connection.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Download_failure_marks_the_connection_failed()
    {
        _github.DownloadTarballAsync("octo", "cat", null, Arg.Any<CancellationToken>())
            .Returns<Task<RepositoryArchive>>(_ => throw new InvalidOperationException("repo not found"));

        await _sut.IngestAsync(_connection.Id, default);

        _connection.Status.ShouldBe(ProcessingStatus.Failed);
        _connection.Error!.ShouldContain("repo not found");
    }

    private static Stream BuildTarGz(string rootPrefix, (string Path, string Content)[] entries)
    {
        var raw = new MemoryStream();
        using (var gz = new GZipStream(raw, CompressionMode.Compress, leaveOpen: true))
        using (var tar = new TarWriter(gz, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                var entry = new PaxTarEntry(TarEntryType.RegularFile, $"{rootPrefix}/{path}")
                {
                    DataStream = new MemoryStream(bytes),
                };
                tar.WriteEntry(entry);
            }
        }

        raw.Position = 0;
        return raw;
    }
}
