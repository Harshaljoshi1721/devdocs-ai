# GitHub Integration (Phase 7) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user connect a public GitHub repository to a project; its supported files are downloaded as a tarball and ingested through the existing chunk/embed/index pipeline so they can be chatted/searched like uploaded files.

**Architecture:** A new `RepositoryConnection` entity tracks the connection + status. A background job downloads the repo tarball via `IGitHubRepositoryClient`, walks its entries, and feeds each supported file into a shared `DocumentIngestor` (extracted from `DocumentService`) that produces `Document`s (tagged with the connection id) which the existing `DocumentProcessor` chunks + embeds. GitHub specifics sit behind a port; no new NuGet dependency (`System.Formats.Tar` + `GZipStream` are built into .NET 10).

**Tech Stack:** .NET 10, EF Core 10 + Npgsql/pgvector, `System.Formats.Tar`, typed `HttpClient`, xUnit + Shouldly + NSubstitute + Testcontainers; Next.js 16 / React 19 / TanStack Query frontend.

**Design source:** [`docs/superpowers/specs/2026-07-28-github-integration-design.md`](../specs/2026-07-28-github-integration-design.md).

> **Git note:** This repo currently has **no commits** and the user gates commits. Before the first commit, confirm the git approach with the user (initial commit? feature branch?). The `git commit` steps below assume that approval; adjust to batch-at-end if the user prefers.

> **Run all backend commands from `backend/`.** Full backend test: `dotnet test`. Frontend commands from `frontend/`. Docker must be running for integration tests (Testcontainers).

---

## File structure

**Domain**
- Create `src/DevDocsAI.Domain/Enums/RepositoryProvider.cs`
- Create `src/DevDocsAI.Domain/Entities/RepositoryConnection.cs`
- Modify `src/DevDocsAI.Domain/Entities/Document.cs` (add `RepositoryConnectionId`)

**Application**
- Create `src/DevDocsAI.Application/Abstractions/AI/…` — no. GitHub port: `src/DevDocsAI.Application/Abstractions/IGitHubRepositoryClient.cs`
- Create `src/DevDocsAI.Application/Features/Repositories/GitHubUrlParser.cs`
- Create `src/DevDocsAI.Application/Features/Repositories/RepoIngestionOptions.cs`
- Create `src/DevDocsAI.Application/Features/Repositories/RepositoryDtos.cs`
- Create `src/DevDocsAI.Application/Features/Repositories/RepositoryConnectionService.cs`
- Create `src/DevDocsAI.Application/Features/Repositories/RepositoryIngestor.cs`
- Create `src/DevDocsAI.Application/Features/Ingestion/DocumentIngestor.cs`
- Modify `src/DevDocsAI.Application/Features/Ingestion/DocumentService.cs` (use `DocumentIngestor`; add `RemoveByConnectionAsync`)
- Modify `src/DevDocsAI.Application/Abstractions/Persistence/IDocumentRepository.cs` (add `ListByConnectionAsync`)
- Modify `src/DevDocsAI.Application/Abstractions/Persistence/Repositories.cs` (add `IRepositoryConnectionRepository`)
- Modify `src/DevDocsAI.Application/DependencyInjection.cs`

**Infrastructure**
- Create `src/DevDocsAI.Infrastructure/GitHub/GitHubOptions.cs`
- Create `src/DevDocsAI.Infrastructure/GitHub/GitHubRepositoryClient.cs`
- Create `src/DevDocsAI.Infrastructure/Persistence/Configurations/RepositoryConnectionConfiguration.cs`
- Modify `src/DevDocsAI.Infrastructure/Persistence/Configurations/DocumentConfiguration.cs`
- Create `src/DevDocsAI.Infrastructure/Persistence/Repositories/RepositoryConnectionRepository.cs`
- Modify `src/DevDocsAI.Infrastructure/Persistence/Repositories/DocumentRepository.cs` (add `ListByConnectionAsync`)
- Modify `src/DevDocsAI.Infrastructure/Persistence/AppDbContext.cs` (DbSet)
- Modify `src/DevDocsAI.Infrastructure/DependencyInjection.cs`
- Migration `AddRepositoryConnections`

**Api**
- Create `src/DevDocsAI.Api/Controllers/RepositoryController.cs`

**Frontend**
- Modify `frontend/src/lib/types.ts`
- Create `frontend/src/components/project-repository.tsx`
- Modify `frontend/src/app/projects/[id]/page.tsx`

**Tests**
- Create `tests/DevDocsAI.UnitTests/Features/GitHubUrlParserTests.cs`
- Create `tests/DevDocsAI.UnitTests/Features/RepositoryConnectionTests.cs`
- Create `tests/DevDocsAI.UnitTests/Features/RepositoryIngestorTests.cs`
- Create `tests/DevDocsAI.UnitTests/Features/RepositoryConnectionServiceTests.cs`
- Create `tests/DevDocsAI.IntegrationTests/Infrastructure/FakeGitHubRepositoryClient.cs`
- Create `tests/DevDocsAI.IntegrationTests/RepositoryEndpointsTests.cs`

---

## Task 1: Domain — `RepositoryConnection` entity + `Document` link

**Files:**
- Create: `src/DevDocsAI.Domain/Enums/RepositoryProvider.cs`
- Create: `src/DevDocsAI.Domain/Entities/RepositoryConnection.cs`
- Modify: `src/DevDocsAI.Domain/Entities/Document.cs`
- Test: `tests/DevDocsAI.UnitTests/Features/RepositoryConnectionTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/DevDocsAI.UnitTests/Features/RepositoryConnectionTests.cs`:
```csharp
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class RepositoryConnectionTests
{
    private readonly Guid _projectId = Guid.CreateVersion7();

    [Fact]
    public void Connect_starts_pending_for_github()
    {
        var c = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/octo/cat", "octo", "cat", "main");

        c.ProjectId.ShouldBe(_projectId);
        c.Provider.ShouldBe(RepositoryProvider.GitHub);
        c.Owner.ShouldBe("octo");
        c.Repo.ShouldBe("cat");
        c.Ref.ShouldBe("main");
        c.Status.ShouldBe(ProcessingStatus.Pending);
        c.CommitSha.ShouldBeNull();
        c.FileCount.ShouldBe(0);
    }

    [Fact]
    public void Lifecycle_marks_processing_then_completed_with_commit_and_count()
    {
        var c = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/octo/cat", "octo", "cat", null);

        c.MarkProcessing();
        c.Status.ShouldBe(ProcessingStatus.Processing);

        c.MarkCompleted("abc123", 42);
        c.Status.ShouldBe(ProcessingStatus.Completed);
        c.CommitSha.ShouldBe("abc123");
        c.FileCount.ShouldBe(42);
        c.Error.ShouldBeNull();
    }

    [Fact]
    public void MarkFailed_records_the_error()
    {
        var c = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/octo/cat", "octo", "cat", null);

        c.MarkFailed("boom");

        c.Status.ShouldBe(ProcessingStatus.Failed);
        c.Error.ShouldBe("boom");
    }

    [Fact]
    public void Reset_returns_to_pending_and_clears_prior_result()
    {
        var c = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/octo/cat", "octo", "cat", null);
        c.MarkCompleted("abc123", 42);

        c.Reset();

        c.Status.ShouldBe(ProcessingStatus.Pending);
        c.CommitSha.ShouldBeNull();
        c.FileCount.ShouldBe(0);
        c.Error.ShouldBeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~RepositoryConnectionTests"`
Expected: FAIL — compile error, `RepositoryConnection`/`RepositoryProvider` do not exist.

- [ ] **Step 3: Create the enum**

Create `src/DevDocsAI.Domain/Enums/RepositoryProvider.cs`:
```csharp
namespace DevDocsAI.Domain.Enums;

/// <summary>Source of a connected repository. Only GitHub is implemented in Phase 7.</summary>
public enum RepositoryProvider
{
    GitHub = 0,
}
```

- [ ] **Step 4: Create the entity**

Create `src/DevDocsAI.Domain/Entities/RepositoryConnection.cs`:
```csharp
using DevDocsAI.Domain.Common;
using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// A project's link to an external source repository. Ingestion produces
/// <see cref="Document"/>s tagged with this connection's id. Status mirrors the
/// document processing lifecycle so the frontend can poll it.
/// </summary>
public sealed class RepositoryConnection : Entity
{
    private RepositoryConnection() { } // EF

    private RepositoryConnection(
        Guid projectId, RepositoryProvider provider, string url, string owner, string repo, string? @ref)
    {
        ProjectId = projectId;
        Provider = provider;
        Url = url;
        Owner = owner;
        Repo = repo;
        Ref = @ref;
        Status = ProcessingStatus.Pending;
    }

    public Guid ProjectId { get; private set; }
    public RepositoryProvider Provider { get; private set; }
    public string Url { get; private set; } = null!;
    public string Owner { get; private set; } = null!;
    public string Repo { get; private set; } = null!;

    /// <summary>Requested branch/tag; null means the repository's default branch.</summary>
    public string? Ref { get; private set; }

    /// <summary>Commit actually ingested; null until a run completes.</summary>
    public string? CommitSha { get; private set; }

    public ProcessingStatus Status { get; private set; }
    public string? Error { get; private set; }
    public int FileCount { get; private set; }

    public static RepositoryConnection Connect(
        Guid projectId, RepositoryProvider provider, string url, string owner, string repo, string? @ref) =>
        new(projectId, provider, url, owner, repo, @ref);

    public void MarkProcessing()
    {
        Status = ProcessingStatus.Processing;
        Error = null;
    }

    public void MarkCompleted(string commitSha, int fileCount)
    {
        Status = ProcessingStatus.Completed;
        CommitSha = commitSha;
        FileCount = fileCount;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Status = ProcessingStatus.Failed;
        Error = error;
    }

    /// <summary>Return to Pending before a re-sync, discarding the previous run's result.</summary>
    public void Reset()
    {
        Status = ProcessingStatus.Pending;
        CommitSha = null;
        FileCount = 0;
        Error = null;
    }
}
```

- [ ] **Step 5: Add the link field to `Document`**

In `src/DevDocsAI.Domain/Entities/Document.cs`, change the constructor to accept an optional connection id and add the property. Replace the constructor signature and body's field list:
```csharp
    public Document(
        Guid projectId,
        string name,
        string path,
        FileType fileType,
        string contentHash,
        long size,
        string storageKey,
        Guid? repositoryConnectionId = null)
    {
        ProjectId = projectId;
        Name = name;
        Path = path;
        FileType = fileType;
        ContentHash = contentHash;
        Size = size;
        StorageKey = storageKey;
        RepositoryConnectionId = repositoryConnectionId;
        ProcessingStatus = ProcessingStatus.Pending;
    }
```
And add this property next to `ProjectId`:
```csharp
    /// <summary>Set when this document came from a connected repository; null for manual uploads.</summary>
    public Guid? RepositoryConnectionId { get; private set; }
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~RepositoryConnectionTests"`
Expected: PASS (4 tests).

- [ ] **Step 7: Commit**
```bash
git add src/DevDocsAI.Domain tests/DevDocsAI.UnitTests/Features/RepositoryConnectionTests.cs
git commit -m "feat(domain): RepositoryConnection entity + Document link"
```

---

## Task 2: `GitHubUrlParser`

**Files:**
- Create: `src/DevDocsAI.Application/Features/Repositories/GitHubUrlParser.cs`
- Test: `tests/DevDocsAI.UnitTests/Features/GitHubUrlParserTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/DevDocsAI.UnitTests/Features/GitHubUrlParserTests.cs`:
```csharp
using DevDocsAI.Application.Features.Repositories;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class GitHubUrlParserTests
{
    [Theory]
    [InlineData("https://github.com/octo/cat", "octo", "cat", null)]
    [InlineData("https://github.com/octo/cat.git", "octo", "cat", null)]
    [InlineData("https://github.com/octo/cat/", "octo", "cat", null)]
    [InlineData("https://www.github.com/octo/cat", "octo", "cat", null)]
    [InlineData("https://github.com/octo/cat/tree/main", "octo", "cat", "main")]
    [InlineData("https://github.com/octo/cat/tree/feature/x", "octo", "cat", "feature/x")]
    public void Parses_valid_public_github_urls(string url, string owner, string repo, string? @ref)
    {
        GitHubUrlParser.TryParse(url, out var result).ShouldBeTrue();
        result!.Owner.ShouldBe(owner);
        result.Repo.ShouldBe(repo);
        result.Ref.ShouldBe(@ref);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("http://github.com/octo/cat")]              // must be https
    [InlineData("https://gitlab.com/octo/cat")]             // wrong host
    [InlineData("git@github.com:octo/cat.git")]             // ssh
    [InlineData("https://github.com/octo")]                 // missing repo
    [InlineData("https://github.com/")]                     // missing both
    [InlineData("https://user:pass@github.com/octo/cat")]   // embedded credentials
    [InlineData("https://github.com/../../etc/passwd")]     // traversal-ish
    public void Rejects_invalid_or_unsafe_urls(string url)
    {
        GitHubUrlParser.TryParse(url, out var result).ShouldBeFalse();
        result.ShouldBeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~GitHubUrlParserTests"`
Expected: FAIL — `GitHubUrlParser` does not exist.

- [ ] **Step 3: Implement the parser**

Create `src/DevDocsAI.Application/Features/Repositories/GitHubUrlParser.cs`:
```csharp
using System.Diagnostics.CodeAnalysis;

namespace DevDocsAI.Application.Features.Repositories;

public sealed record GitHubRepoRef(string Owner, string Repo, string? Ref);

/// <summary>
/// Parses and validates a public GitHub HTTPS repository URL. Only github.com is
/// accepted (SSRF guard); SSH, other hosts, and embedded credentials are rejected.
/// </summary>
public static class GitHubUrlParser
{
    private static readonly HashSet<string> AllowedHosts =
        new(StringComparer.OrdinalIgnoreCase) { "github.com", "www.github.com" };

    public static bool TryParse(string? url, [NotNullWhen(true)] out GitHubRepoRef? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;

        if (uri.Scheme != Uri.UriSchemeHttps) return false;
        if (!AllowedHosts.Contains(uri.Host)) return false;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return false; // no user:pass@

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2) return false;

        var owner = segments[0];
        var repo = segments[1];
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            repo = repo[..^4];

        if (!IsSegment(owner) || !IsSegment(repo)) return false;

        string? @ref = null;
        // .../tree/{ref...} — ref may itself contain slashes (feature/x).
        if (segments.Length >= 4 && segments[2].Equals("tree", StringComparison.OrdinalIgnoreCase))
        {
            @ref = string.Join('/', segments[3..]);
            if (@ref.Contains("..")) return false;
        }

        result = new GitHubRepoRef(owner, repo, @ref);
        return true;
    }

    // GitHub owner/repo names: letters, digits, '-', '_', '.'; no traversal.
    private static bool IsSegment(string s) =>
        s.Length > 0 && s != "." && s != ".." &&
        s.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.');
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~GitHubUrlParserTests"`
Expected: PASS (14 cases).

- [ ] **Step 5: Commit**
```bash
git add src/DevDocsAI.Application/Features/Repositories/GitHubUrlParser.cs tests/DevDocsAI.UnitTests/Features/GitHubUrlParserTests.cs
git commit -m "feat(app): GitHub repo URL parser + validation"
```

---

## Task 3: Port, options, and DTOs

**Files:**
- Create: `src/DevDocsAI.Application/Abstractions/IGitHubRepositoryClient.cs`
- Create: `src/DevDocsAI.Application/Features/Repositories/RepoIngestionOptions.cs`
- Create: `src/DevDocsAI.Application/Features/Repositories/RepositoryDtos.cs`

No test (interfaces/records/options only; exercised by later tasks).

- [ ] **Step 1: Create the GitHub client port**

Create `src/DevDocsAI.Application/Abstractions/IGitHubRepositoryClient.cs`:
```csharp
namespace DevDocsAI.Application.Abstractions;

/// <summary>A downloaded repository snapshot: the resolved commit and a gzip'd tar stream of the tree.</summary>
public sealed record RepositoryArchive(string CommitSha, Stream Content) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        if (Content is not null) await Content.DisposeAsync();
    }
}

/// <summary>Retrieves public repository contents. Provider-swappable (only GitHub in Phase 7).</summary>
public interface IGitHubRepositoryClient
{
    /// <summary>Downloads the repo at <paramref name="ref"/> (or the default branch) as a tar.gz stream.</summary>
    Task<RepositoryArchive> DownloadTarballAsync(string owner, string repo, string? @ref, CancellationToken ct);
}
```

- [ ] **Step 2: Create the ingestion options**

Create `src/DevDocsAI.Application/Features/Repositories/RepoIngestionOptions.cs`:
```csharp
namespace DevDocsAI.Application.Features.Repositories;

/// <summary>Caps applied when ingesting a repository, bound from the "RepoIngestion" section.</summary>
public sealed class RepoIngestionOptions
{
    public const string SectionName = "RepoIngestion";

    /// <summary>Maximum number of supported files ingested from one repository.</summary>
    public int MaxFiles { get; init; } = 1500;

    /// <summary>Maximum total decompressed bytes read before aborting (tar-bomb guard). 25 MB.</summary>
    public long MaxTotalBytes { get; init; } = 25L * 1024 * 1024;

    /// <summary>Maximum size of a single file. 5 MB (matches the upload cap).</summary>
    public long MaxFileBytes { get; init; } = 5L * 1024 * 1024;
}
```

- [ ] **Step 3: Create the DTOs**

Create `src/DevDocsAI.Application/Features/Repositories/RepositoryDtos.cs`:
```csharp
namespace DevDocsAI.Application.Features.Repositories;

public sealed record ConnectRepositoryRequest(string Url, string? Ref);

public sealed record RepositoryConnectionResponse(
    Guid Id,
    Guid ProjectId,
    string Provider,
    string Url,
    string Owner,
    string Repo,
    string? Ref,
    string? CommitSha,
    string Status,
    string? Error,
    int FileCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build src/DevDocsAI.Application/DevDocsAI.Application.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 5: Commit**
```bash
git add src/DevDocsAI.Application/Abstractions/IGitHubRepositoryClient.cs src/DevDocsAI.Application/Features/Repositories/RepoIngestionOptions.cs src/DevDocsAI.Application/Features/Repositories/RepositoryDtos.cs
git commit -m "feat(app): GitHub client port, ingestion options, DTOs"
```

---

## Task 4: Extract `DocumentIngestor` (shared per-file ingestion)

**Files:**
- Create: `src/DevDocsAI.Application/Features/Ingestion/DocumentIngestor.cs`
- Modify: `src/DevDocsAI.Application/Features/Ingestion/DocumentService.cs`
- Modify: `src/DevDocsAI.Application/Abstractions/Persistence/IDocumentRepository.cs`
- Modify: `src/DevDocsAI.Application/DependencyInjection.cs`

This refactor is covered by the existing `DocumentEndpointsTests` integration tests (uploads must keep working) — no new unit test. Verify by running them at the end.

- [ ] **Step 1: Add `ListByConnectionAsync` to the document repo port**

In `src/DevDocsAI.Application/Abstractions/Persistence/IDocumentRepository.cs`, add inside the interface:
```csharp
    Task<IReadOnlyList<Document>> ListByConnectionAsync(Guid repositoryConnectionId, CancellationToken ct);
```

- [ ] **Step 2: Create the shared ingestor**

Create `src/DevDocsAI.Application/Features/Ingestion/DocumentIngestor.cs`:
```csharp
using DevDocsAI.Application.Abstractions;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Abstractions.Storage;
using DevDocsAI.Application.Features.Processing;
using DevDocsAI.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace DevDocsAI.Application.Features.Ingestion;

/// <summary>Outcome of ingesting a single file: the new document id, or a rejection reason.</summary>
public sealed record IngestOutcome(Guid? DocumentId, string? RejectionReason)
{
    public static IngestOutcome Accepted(Guid id) => new(id, null);
    public static IngestOutcome Rejected(string reason) => new(null, reason);
}

/// <summary>
/// The per-file half of ingestion, shared by manual upload and repository import:
/// screen → store → content-hash dedupe → create a <see cref="Document"/> (added to
/// the unit of work, not yet saved). Callers batch the save and enqueue processing.
/// </summary>
public interface IDocumentIngestor
{
    Task<IngestOutcome> IngestAsync(
        Guid projectId, string path, long length, Stream content,
        Guid? repositoryConnectionId, ISet<string> seenHashes, CancellationToken ct);

    /// <summary>Enqueue the background chunk/embed pipeline for each accepted document.</summary>
    Task EnqueueProcessingAsync(IReadOnlyList<Guid> documentIds, CancellationToken ct);
}

public sealed class DocumentIngestor(
    IDocumentRepository documents,
    IFileStorage fileStorage,
    IFileFilter fileFilter,
    IBackgroundTaskQueue queue) : IDocumentIngestor
{
    public async Task<IngestOutcome> IngestAsync(
        Guid projectId, string path, long length, Stream content,
        Guid? repositoryConnectionId, ISet<string> seenHashes, CancellationToken ct)
    {
        if (length <= 0) return IngestOutcome.Rejected("empty");
        if (fileFilter.IsSecret(path)) return IngestOutcome.Rejected("secret");
        if (!fileFilter.IsSupported(path)) return IngestOutcome.Rejected("unsupported");

        var extension = Path.GetExtension(Path.GetFileName(path));
        var stored = await fileStorage.SaveAsync(projectId, extension, content, ct);

        if (!seenHashes.Add(stored.ContentHash) ||
            await documents.ExistsByHashAsync(projectId, stored.ContentHash, ct))
        {
            await fileStorage.DeleteAsync(stored.StorageKey, ct);
            return IngestOutcome.Rejected("duplicate");
        }

        var document = new Document(
            projectId,
            name: Path.GetFileName(path),
            path: path,
            fileType: fileFilter.Categorize(path),
            contentHash: stored.ContentHash,
            size: stored.SizeBytes,
            storageKey: stored.StorageKey,
            repositoryConnectionId: repositoryConnectionId);

        await documents.AddAsync(document, ct);
        return IngestOutcome.Accepted(document.Id);
    }

    public async Task EnqueueProcessingAsync(IReadOnlyList<Guid> documentIds, CancellationToken ct)
    {
        foreach (var id in documentIds)
        {
            await queue.EnqueueAsync(
                (sp, token) => new ValueTask(sp.GetRequiredService<IDocumentProcessor>().ProcessAsync(id, token)),
                ct);
        }
    }
}
```

- [ ] **Step 3: Refactor `DocumentService` to use the ingestor + add connection cleanup**

Replace the whole body of `src/DevDocsAI.Application/Features/Ingestion/DocumentService.cs` with:
```csharp
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Abstractions.Storage;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Domain.Entities;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Application.Features.Ingestion;

public interface IDocumentService
{
    Task<UploadResult> UploadAsync(Guid userId, Guid projectId, IReadOnlyList<UploadFileInput> files, CancellationToken ct);
    Task<IReadOnlyList<DocumentResponse>> ListAsync(Guid userId, Guid projectId, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid projectId, Guid documentId, CancellationToken ct);

    /// <summary>Removes all documents ingested from a repository connection (used by re-sync/disconnect).</summary>
    Task RemoveByConnectionAsync(Guid repositoryConnectionId, CancellationToken ct);
}

public sealed class DocumentService(
    IProjectRepository projects,
    IDocumentRepository documents,
    IFileStorage fileStorage,
    IDocumentIngestor ingestor,
    IUnitOfWork unitOfWork,
    IOptions<UploadOptions> uploadOptions) : IDocumentService
{
    private readonly UploadOptions _options = uploadOptions.Value;

    public async Task<UploadResult> UploadAsync(
        Guid userId, Guid projectId, IReadOnlyList<UploadFileInput> files, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);

        if (files.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["files"] = ["At least one file is required."],
            });
        }

        if (files.Count > _options.MaxFilesPerRequest)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["files"] = [$"A maximum of {_options.MaxFilesPerRequest} files may be uploaded at once."],
            });
        }

        var accepted = new List<DocumentResponse>();
        var rejected = new List<RejectedFile>();
        var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var acceptedIds = new List<Guid>();

        foreach (var file in files)
        {
            if (file.Length > _options.MaxFileSizeBytes)
            {
                rejected.Add(new RejectedFile(file.FileName, "too_large"));
                continue;
            }

            var outcome = await ingestor.IngestAsync(
                projectId, file.FileName, file.Length, file.Content, null, seenHashes, ct);

            if (outcome.DocumentId is { } id)
            {
                var doc = await documents.GetByIdAsync(id, ct);
                accepted.Add(Map(doc!));
                acceptedIds.Add(id);
            }
            else
            {
                rejected.Add(new RejectedFile(file.FileName, outcome.RejectionReason!));
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        await ingestor.EnqueueProcessingAsync(acceptedIds, ct);

        return new UploadResult(accepted, rejected);
    }

    public async Task<IReadOnlyList<DocumentResponse>> ListAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);
        var docs = await documents.ListByProjectAsync(projectId, ct);
        return docs.Select(Map).ToList();
    }

    public async Task DeleteAsync(Guid userId, Guid projectId, Guid documentId, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);

        var document = await documents.GetByIdAsync(documentId, ct);
        if (document is null || document.ProjectId != projectId)
        {
            throw new NotFoundException("Document not found.");
        }

        documents.Remove(document);
        await unitOfWork.SaveChangesAsync(ct);
        await fileStorage.DeleteAsync(document.StorageKey, ct);
    }

    public async Task RemoveByConnectionAsync(Guid repositoryConnectionId, CancellationToken ct)
    {
        var docs = await documents.ListByConnectionAsync(repositoryConnectionId, ct);
        foreach (var doc in docs)
        {
            documents.Remove(doc);
        }

        await unitOfWork.SaveChangesAsync(ct);

        foreach (var doc in docs)
        {
            await fileStorage.DeleteAsync(doc.StorageKey, ct);
        }
    }

    private async Task EnsureProjectOwnedAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null || project.OwnerId != userId)
        {
            throw new NotFoundException("Project not found.");
        }
    }

    private static DocumentResponse Map(Document d) => new(
        d.Id, d.Name, d.Path, d.FileType.ToString(), d.Size, d.ContentHash,
        d.ProcessingStatus.ToString(), d.Error, d.CreatedAt, d.UpdatedAt);
}
```

Note: `UploadFileInput.Content` streams are still screened for size before ingest. The empty/secret/unsupported checks now live in the ingestor (single source of truth).

- [ ] **Step 4: Register `IDocumentIngestor` in DI**

In `src/DevDocsAI.Application/DependencyInjection.cs`, add next to the other `AddScoped` lines:
```csharp
        services.AddScoped<IDocumentIngestor, DocumentIngestor>();
```

- [ ] **Step 5: Build**

Run: `dotnet build src/DevDocsAI.Api/DevDocsAI.Api.csproj`
Expected: Build succeeded, 0 warnings. (Repository interface method is implemented in Task 6; if the build fails only because `ListByConnectionAsync` is unimplemented, proceed to Task 6 then re-run — but prefer implementing Step 6 below first.)

- [ ] **Step 6: Implement `ListByConnectionAsync` in the repository**

In `src/DevDocsAI.Infrastructure/Persistence/Repositories/DocumentRepository.cs`, add:
```csharp
    public async Task<IReadOnlyList<Document>> ListByConnectionAsync(Guid repositoryConnectionId, CancellationToken ct) =>
        await db.Documents.Where(d => d.RepositoryConnectionId == repositoryConnectionId).ToListAsync(ct);
```

- [ ] **Step 7: Build again**

Run: `dotnet build src/DevDocsAI.Api/DevDocsAI.Api.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 8: Commit**
```bash
git add src/DevDocsAI.Application src/DevDocsAI.Infrastructure/Persistence/Repositories/DocumentRepository.cs
git commit -m "refactor(app): extract DocumentIngestor shared by upload + repo import"
```

---

## Task 5: Persistence — EF config, DbSet, migration, repository

**Files:**
- Create: `src/DevDocsAI.Infrastructure/Persistence/Configurations/RepositoryConnectionConfiguration.cs`
- Modify: `src/DevDocsAI.Infrastructure/Persistence/Configurations/DocumentConfiguration.cs`
- Modify: `src/DevDocsAI.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `src/DevDocsAI.Application/Abstractions/Persistence/Repositories.cs`
- Create: `src/DevDocsAI.Infrastructure/Persistence/Repositories/RepositoryConnectionRepository.cs`
- Modify: `src/DevDocsAI.Infrastructure/DependencyInjection.cs`
- Migration: `AddRepositoryConnections`

- [ ] **Step 1: Add the repository port**

In `src/DevDocsAI.Application/Abstractions/Persistence/Repositories.cs`, add:
```csharp
public interface IRepositoryConnectionRepository
{
    Task<RepositoryConnection?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<RepositoryConnection?> GetByProjectAsync(Guid projectId, CancellationToken ct);
    Task AddAsync(RepositoryConnection connection, CancellationToken ct);
    void Remove(RepositoryConnection connection);
}
```

- [ ] **Step 2: EF configuration for the entity**

Create `src/DevDocsAI.Infrastructure/Persistence/Configurations/RepositoryConnectionConfiguration.cs`:
```csharp
using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevDocsAI.Infrastructure.Persistence.Configurations;

public sealed class RepositoryConnectionConfiguration : IEntityTypeConfiguration<RepositoryConnection>
{
    public void Configure(EntityTypeBuilder<RepositoryConnection> builder)
    {
        builder.ToTable("repository_connections");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever(); // app-assigned UUID v7

        builder.Property(c => c.Provider).HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.Url).HasMaxLength(2048).IsRequired();
        builder.Property(c => c.Owner).HasMaxLength(256).IsRequired();
        builder.Property(c => c.Repo).HasMaxLength(256).IsRequired();
        builder.Property(c => c.Ref).HasMaxLength(256);
        builder.Property(c => c.CommitSha).HasMaxLength(64);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.Error).HasMaxLength(4000);

        // One connection per project.
        builder.HasIndex(c => c.ProjectId).IsUnique();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 3: Configure the `Document` FK**

In `src/DevDocsAI.Infrastructure/Persistence/Configurations/DocumentConfiguration.cs`, add before the closing brace of `Configure` (after the existing `HasOne<Project>()` block):
```csharp
        builder.HasIndex(d => d.RepositoryConnectionId);
        builder.HasOne<RepositoryConnection>()
            .WithMany()
            .HasForeignKey(d => d.RepositoryConnectionId)
            .OnDelete(DeleteBehavior.NoAction); // documents are removed explicitly before the connection
```

- [ ] **Step 4: Add DbSet**

In `src/DevDocsAI.Infrastructure/Persistence/AppDbContext.cs`, add next to the other `DbSet`s:
```csharp
    public DbSet<RepositoryConnection> RepositoryConnections => Set<RepositoryConnection>();
```

- [ ] **Step 5: Implement the repository**

Create `src/DevDocsAI.Infrastructure/Persistence/Repositories/RepositoryConnectionRepository.cs`:
```csharp
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocsAI.Infrastructure.Persistence.Repositories;

public sealed class RepositoryConnectionRepository(AppDbContext db) : IRepositoryConnectionRepository
{
    public Task<RepositoryConnection?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.RepositoryConnections.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<RepositoryConnection?> GetByProjectAsync(Guid projectId, CancellationToken ct) =>
        db.RepositoryConnections.FirstOrDefaultAsync(c => c.ProjectId == projectId, ct);

    public async Task AddAsync(RepositoryConnection connection, CancellationToken ct) =>
        await db.RepositoryConnections.AddAsync(connection, ct);

    public void Remove(RepositoryConnection connection) => db.RepositoryConnections.Remove(connection);
}
```

- [ ] **Step 6: Register the repository in DI**

In `src/DevDocsAI.Infrastructure/DependencyInjection.cs`, add next to the other persistence ports:
```csharp
        services.AddScoped<IRepositoryConnectionRepository, RepositoryConnectionRepository>();
```

- [ ] **Step 7: Generate the migration**

Run:
```bash
dotnet ef migrations add AddRepositoryConnections --project src/DevDocsAI.Infrastructure --startup-project src/DevDocsAI.Api --output-dir Persistence/Migrations
```
Expected: "Done." Open the generated `*_AddRepositoryConnections.cs` and confirm it creates `repository_connections` (unique index on `ProjectId`) and adds `RepositoryConnectionId` + index to `documents`. No other tables should change.

- [ ] **Step 8: Build**

Run: `dotnet build src/DevDocsAI.Api/DevDocsAI.Api.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 9: Commit**
```bash
git add src/DevDocsAI.Application/Abstractions/Persistence/Repositories.cs src/DevDocsAI.Infrastructure
git commit -m "feat(infra): persistence for RepositoryConnection + migration"
```

---

## Task 6: `RepositoryIngestor` (tarball walk)

**Files:**
- Create: `src/DevDocsAI.Application/Features/Repositories/RepositoryIngestor.cs`
- Test: `tests/DevDocsAI.UnitTests/Features/RepositoryIngestorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/DevDocsAI.UnitTests/Features/RepositoryIngestorTests.cs`:
```csharp
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

        // Accept anything the ingestor is asked to ingest, returning a fresh id.
        _ingestor.IngestAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<Stream>(),
                Arg.Any<Guid?>(), Arg.Any<ISet<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => IngestOutcome.Accepted(Guid.CreateVersion7()));
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
            ("logo.png", "binarybytes"),          // unsupported → skipped before ingest
            (".env", "SECRET=1"));                // secret → not even passed to ingestor

        await _sut.IngestAsync(_connection.Id, default);

        _connection.Status.ShouldBe(ProcessingStatus.Completed);
        _connection.CommitSha.ShouldBe("sha1");
        _connection.FileCount.ShouldBe(2);

        // Only the two supported, non-secret files reach the ingestor, with repo-relative paths.
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
        _connection.Error.ShouldContain("repo not found");
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~RepositoryIngestorTests"`
Expected: FAIL — `RepositoryIngestor` does not exist.

- [ ] **Step 3: Implement the ingestor**

Create `src/DevDocsAI.Application/Features/Repositories/RepositoryIngestor.cs`:
```csharp
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
                if (outcome.DocumentId is { } id) acceptedIds.Add(id);
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~RepositoryIngestorTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**
```bash
git add src/DevDocsAI.Application/Features/Repositories/RepositoryIngestor.cs tests/DevDocsAI.UnitTests/Features/RepositoryIngestorTests.cs
git commit -m "feat(app): RepositoryIngestor tarball walk + caps + filtering"
```

---

## Task 7: `RepositoryConnectionService`

**Files:**
- Create: `src/DevDocsAI.Application/Features/Repositories/RepositoryConnectionService.cs`
- Modify: `src/DevDocsAI.Application/DependencyInjection.cs`
- Test: `tests/DevDocsAI.UnitTests/Features/RepositoryConnectionServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/DevDocsAI.UnitTests/Features/RepositoryConnectionServiceTests.cs`:
```csharp
using DevDocsAI.Application.Abstractions;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Ingestion;
using DevDocsAI.Application.Features.Repositories;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class RepositoryConnectionServiceTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IRepositoryConnectionRepository _connections = Substitute.For<IRepositoryConnectionRepository>();
    private readonly IDocumentService _documents = Substitute.For<IDocumentService>();
    private readonly IBackgroundTaskQueue _queue = Substitute.For<IBackgroundTaskQueue>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly RepositoryConnectionService _sut;

    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly Guid _projectId = Guid.CreateVersion7();

    public RepositoryConnectionServiceTests()
    {
        _sut = new RepositoryConnectionService(_projects, _connections, _documents, _queue, _uow);
        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, _userId));
    }

    [Fact]
    public async Task Connect_creates_a_pending_connection_and_enqueues_ingestion()
    {
        var response = await _sut.ConnectAsync(
            _userId, _projectId, new ConnectRepositoryRequest("https://github.com/octo/cat", null), default);

        response.Owner.ShouldBe("octo");
        response.Repo.ShouldBe("cat");
        response.Status.ShouldBe(nameof(ProcessingStatus.Pending));
        await _connections.Received(1).AddAsync(Arg.Any<RepositoryConnection>(), Arg.Any<CancellationToken>());
        await _uow.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _queue.Received(1).EnqueueAsync(Arg.Any<Func<IServiceProvider, CancellationToken, ValueTask>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Connect_replaces_an_existing_connection()
    {
        var existing = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/old/repo", "old", "repo", null);
        _connections.GetByProjectAsync(_projectId, Arg.Any<CancellationToken>()).Returns(existing);

        await _sut.ConnectAsync(
            _userId, _projectId, new ConnectRepositoryRequest("https://github.com/octo/cat", null), default);

        await _documents.Received(1).RemoveByConnectionAsync(existing.Id, Arg.Any<CancellationToken>());
        _connections.Received(1).Remove(existing);
    }

    [Fact]
    public async Task Connect_with_an_invalid_url_is_a_validation_error()
    {
        await Should.ThrowAsync<ValidationException>(() => _sut.ConnectAsync(
            _userId, _projectId, new ConnectRepositoryRequest("not-a-repo", null), default));
    }

    [Fact]
    public async Task Connect_on_another_users_project_is_not_found()
    {
        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, Guid.CreateVersion7()));

        await Should.ThrowAsync<NotFoundException>(() => _sut.ConnectAsync(
            _userId, _projectId, new ConnectRepositoryRequest("https://github.com/octo/cat", null), default));
    }

    [Fact]
    public async Task Get_returns_null_when_no_connection_exists()
    {
        var result = await _sut.GetAsync(_userId, _projectId, default);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Disconnect_removes_docs_and_connection()
    {
        var existing = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/octo/cat", "octo", "cat", null);
        _connections.GetByProjectAsync(_projectId, Arg.Any<CancellationToken>()).Returns(existing);

        await _sut.DisconnectAsync(_userId, _projectId, default);

        await _documents.Received(1).RemoveByConnectionAsync(existing.Id, Arg.Any<CancellationToken>());
        _connections.Received(1).Remove(existing);
        await _uow.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~RepositoryConnectionServiceTests"`
Expected: FAIL — `RepositoryConnectionService` does not exist.

- [ ] **Step 3: Implement the service**

Create `src/DevDocsAI.Application/Features/Repositories/RepositoryConnectionService.cs`:
```csharp
using DevDocsAI.Application.Abstractions;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Ingestion;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace DevDocsAI.Application.Features.Repositories;

public interface IRepositoryConnectionService
{
    Task<RepositoryConnectionResponse> ConnectAsync(Guid userId, Guid projectId, ConnectRepositoryRequest request, CancellationToken ct);
    Task<RepositoryConnectionResponse?> GetAsync(Guid userId, Guid projectId, CancellationToken ct);
    Task<RepositoryConnectionResponse> ResyncAsync(Guid userId, Guid projectId, CancellationToken ct);
    Task DisconnectAsync(Guid userId, Guid projectId, CancellationToken ct);
}

/// <summary>
/// Manages a project's single repository connection: validate + create (replacing
/// any existing), report status, re-sync, and disconnect. Ingestion runs in the
/// background via <see cref="IRepositoryIngestor"/>.
/// </summary>
public sealed class RepositoryConnectionService(
    IProjectRepository projects,
    IRepositoryConnectionRepository connections,
    IDocumentService documents,
    IBackgroundTaskQueue queue,
    IUnitOfWork uow) : IRepositoryConnectionService
{
    public async Task<RepositoryConnectionResponse> ConnectAsync(
        Guid userId, Guid projectId, ConnectRepositoryRequest request, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);

        if (!GitHubUrlParser.TryParse(request.Url, out var repo))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["url"] = ["Enter a valid public GitHub repository URL, e.g. https://github.com/owner/repo."],
            });
        }

        // Replace any existing connection for this project. Commit the deletion in
        // its own SaveChanges before inserting the new row — otherwise EF may order
        // the insert before the delete and violate the unique index on ProjectId.
        var existing = await connections.GetByProjectAsync(projectId, ct);
        if (existing is not null)
        {
            await documents.RemoveByConnectionAsync(existing.Id, ct);
            connections.Remove(existing);
            await uow.SaveChangesAsync(ct);
        }

        var @ref = string.IsNullOrWhiteSpace(request.Ref) ? repo.Ref : request.Ref.Trim();
        var connection = RepositoryConnection.Connect(
            projectId, RepositoryProvider.GitHub,
            $"https://github.com/{repo.Owner}/{repo.Repo}", repo.Owner, repo.Repo, @ref);

        await connections.AddAsync(connection, ct);
        await uow.SaveChangesAsync(ct);
        await EnqueueIngestionAsync(connection.Id, ct);

        return Map(connection);
    }

    public async Task<RepositoryConnectionResponse?> GetAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);
        var connection = await connections.GetByProjectAsync(projectId, ct);
        return connection is null ? null : Map(connection);
    }

    public async Task<RepositoryConnectionResponse> ResyncAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);
        var connection = await connections.GetByProjectAsync(projectId, ct)
            ?? throw new NotFoundException("No repository is connected to this project.");

        connection.Reset();
        await uow.SaveChangesAsync(ct);
        await EnqueueIngestionAsync(connection.Id, ct);
        return Map(connection);
    }

    public async Task DisconnectAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);
        var connection = await connections.GetByProjectAsync(projectId, ct);
        if (connection is null) return;

        await documents.RemoveByConnectionAsync(connection.Id, ct);
        connections.Remove(connection);
        await uow.SaveChangesAsync(ct);
    }

    private async Task EnqueueIngestionAsync(Guid connectionId, CancellationToken ct) =>
        await queue.EnqueueAsync(
            (sp, token) => new ValueTask(sp.GetRequiredService<IRepositoryIngestor>().IngestAsync(connectionId, token)),
            ct);

    private async Task EnsureProjectOwnedAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(projectId, ct);
        if (project is null || project.OwnerId != userId)
        {
            throw new NotFoundException("Project not found.");
        }
    }

    private static RepositoryConnectionResponse Map(RepositoryConnection c) => new(
        c.Id, c.ProjectId, c.Provider.ToString(), c.Url, c.Owner, c.Repo, c.Ref, c.CommitSha,
        c.Status.ToString(), c.Error, c.FileCount, c.CreatedAt, c.UpdatedAt);
}
```

- [ ] **Step 4: Register services in DI**

In `src/DevDocsAI.Application/DependencyInjection.cs`, add:
```csharp
        services.AddScoped<IRepositoryConnectionService, RepositoryConnectionService>();
        services.AddScoped<IRepositoryIngestor, RepositoryIngestor>();
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~RepositoryConnectionServiceTests"`
Expected: PASS (6 tests).

- [ ] **Step 6: Commit**
```bash
git add src/DevDocsAI.Application tests/DevDocsAI.UnitTests/Features/RepositoryConnectionServiceTests.cs
git commit -m "feat(app): RepositoryConnectionService (connect/get/resync/disconnect)"
```

---

## Task 8: GitHub adapter + options + DI

**Files:**
- Create: `src/DevDocsAI.Infrastructure/GitHub/GitHubOptions.cs`
- Create: `src/DevDocsAI.Infrastructure/GitHub/GitHubRepositoryClient.cs`
- Modify: `src/DevDocsAI.Infrastructure/DependencyInjection.cs`
- Modify: `src/DevDocsAI.Api/appsettings.json`

No unit test (network adapter; verified via the fake in integration tests). Keep it small and correct.

- [ ] **Step 1: Options**

Create `src/DevDocsAI.Infrastructure/GitHub/GitHubOptions.cs`:
```csharp
namespace DevDocsAI.Infrastructure.GitHub;

/// <summary>GitHub endpoints for public repo download, bound from the "GitHub" section. No secrets.</summary>
public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>Codeload host that serves repository tarballs.</summary>
    public string CodeloadBaseUrl { get; init; } = "https://codeload.github.com/";

    /// <summary>REST API base (used to resolve the default branch / commit sha).</summary>
    public string ApiBaseUrl { get; init; } = "https://api.github.com/";

    public int TimeoutSeconds { get; init; } = 100;
}
```

- [ ] **Step 2: Adapter**

Create `src/DevDocsAI.Infrastructure/GitHub/GitHubRepositoryClient.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DevDocsAI.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Infrastructure.GitHub;

/// <summary>Downloads a public GitHub repository as a tar.gz via codeload, resolving the commit first.</summary>
public sealed class GitHubRepositoryClient(HttpClient http, IOptions<GitHubOptions> options)
    : IGitHubRepositoryClient
{
    private readonly GitHubOptions _options = options.Value;

    public async Task<RepositoryArchive> DownloadTarballAsync(
        string owner, string repo, string? @ref, CancellationToken ct)
    {
        // Resolve the ref to a concrete commit (also validates existence / public access).
        var commit = await ResolveCommitAsync(owner, repo, @ref, ct);

        var url = $"{_options.CodeloadBaseUrl.TrimEnd('/')}/{owner}/{repo}/tar.gz/{commit}";
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Could not reach GitHub to download {owner}/{repo}.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new InvalidOperationException(
                $"Failed to download {owner}/{repo} ({(int)response.StatusCode}).");
        }

        var stream = await response.Content.ReadAsStreamAsync(ct);
        return new RepositoryArchive(commit, stream);
    }

    private async Task<string> ResolveCommitAsync(string owner, string repo, string? @ref, CancellationToken ct)
    {
        // GET /repos/{owner}/{repo}/commits/{ref|HEAD} → the resolved commit sha.
        var reference = string.IsNullOrWhiteSpace(@ref) ? "HEAD" : @ref;
        var url = $"{_options.ApiBaseUrl.TrimEnd('/')}/repos/{owner}/{repo}/commits/{reference}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("DevDocsAI");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Could not reach GitHub for {owner}/{repo}.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidOperationException(
                    $"Repository {owner}/{repo} was not found. It must be public and the branch must exist.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new InvalidOperationException("GitHub rate limit reached. Try again later.");
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"GitHub returned {(int)response.StatusCode} for {owner}/{repo}.");

            var body = await response.Content.ReadFromJsonAsync<CommitResponse>(ct);
            if (string.IsNullOrEmpty(body?.Sha))
                throw new InvalidOperationException($"GitHub did not return a commit for {owner}/{repo}.");
            return body.Sha;
        }
    }

    private sealed record CommitResponse([property: JsonPropertyName("sha")] string Sha);
}
```

- [ ] **Step 3: Register the adapter + options + config**

In `src/DevDocsAI.Infrastructure/DependencyInjection.cs`, add (near the other `AddHttpClient`/options registrations):
```csharp
        services.AddOptions<GitHubOptions>().Bind(configuration.GetSection(GitHubOptions.SectionName));
        services.AddHttpClient<IGitHubRepositoryClient, GitHubRepositoryClient>()
            .ConfigureHttpClient((sp, c) =>
            {
                var opt = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
                c.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
            });
        services.AddOptions<RepoIngestionOptions>().Bind(configuration.GetSection(RepoIngestionOptions.SectionName));
```
Add the required usings at the top of the file if missing:
```csharp
using DevDocsAI.Application.Features.Repositories;
using DevDocsAI.Infrastructure.GitHub;
using Microsoft.Extensions.Options;
```

In `src/DevDocsAI.Api/appsettings.json`, add a top-level section (non-secret defaults; the options classes already carry defaults, so this is optional but explicit):
```json
  "GitHub": {
    "CodeloadBaseUrl": "https://codeload.github.com/",
    "ApiBaseUrl": "https://api.github.com/"
  },
  "RepoIngestion": {
    "MaxFiles": 1500,
    "MaxTotalBytes": 26214400,
    "MaxFileBytes": 5242880
  },
```

- [ ] **Step 4: Build the whole backend**

Run: `dotnet build`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 5: Commit**
```bash
git add src/DevDocsAI.Infrastructure src/DevDocsAI.Api/appsettings.json
git commit -m "feat(infra): GitHubRepositoryClient adapter + options + DI"
```

---

## Task 9: `RepositoryController`

**Files:**
- Create: `src/DevDocsAI.Api/Controllers/RepositoryController.cs`

- [ ] **Step 1: Create the controller**

Create `src/DevDocsAI.Api/Controllers/RepositoryController.cs`:
```csharp
using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevDocsAI.Api.Controllers;

/// <summary>Connect a public GitHub repository to a project and ingest it (Phase 7).</summary>
[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/repository")]
public sealed class RepositoryController(IRepositoryConnectionService repositories, ICurrentUser currentUser)
    : ControllerBase
{
    private Guid UserId => currentUser.UserId
        ?? throw new AuthenticationException("The request is not authenticated.");

    [HttpPost]
    public async Task<ActionResult<RepositoryConnectionResponse>> Connect(
        Guid projectId, ConnectRepositoryRequest request, CancellationToken ct)
    {
        var connection = await repositories.ConnectAsync(UserId, projectId, request, ct);
        return AcceptedAtAction(nameof(Get), new { projectId }, connection);
    }

    [HttpGet]
    public async Task<ActionResult<RepositoryConnectionResponse>> Get(Guid projectId, CancellationToken ct)
    {
        var connection = await repositories.GetAsync(UserId, projectId, ct);
        return connection is null ? NotFound() : Ok(connection);
    }

    [HttpPost("resync")]
    public async Task<ActionResult<RepositoryConnectionResponse>> Resync(Guid projectId, CancellationToken ct)
        => Accepted(await repositories.ResyncAsync(UserId, projectId, ct));

    [HttpDelete]
    public async Task<IActionResult> Disconnect(Guid projectId, CancellationToken ct)
    {
        await repositories.DisconnectAsync(UserId, projectId, ct);
        return NoContent();
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/DevDocsAI.Api/DevDocsAI.Api.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**
```bash
git add src/DevDocsAI.Api/Controllers/RepositoryController.cs
git commit -m "feat(api): repository connection controller"
```

---

## Task 10: Integration tests (fake GitHub client)

**Files:**
- Create: `tests/DevDocsAI.IntegrationTests/Infrastructure/FakeGitHubRepositoryClient.cs`
- Modify: `tests/DevDocsAI.IntegrationTests/Infrastructure/DevDocsApiFactory.cs`
- Create: `tests/DevDocsAI.IntegrationTests/RepositoryEndpointsTests.cs`

- [ ] **Step 1: Add a fake GitHub client that serves an in-memory tar.gz**

Create `tests/DevDocsAI.IntegrationTests/Infrastructure/FakeGitHubRepositoryClient.cs`:
```csharp
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
```

- [ ] **Step 2: Register the fake in the test factory**

In `tests/DevDocsAI.IntegrationTests/Infrastructure/DevDocsApiFactory.cs`, inside the existing `ConfigureTestServices(services => { ... })` block, add:
```csharp
            services.RemoveAll<IGitHubRepositoryClient>();
            services.AddSingleton<IGitHubRepositoryClient, FakeGitHubRepositoryClient>();
```
Add the using if missing: `using DevDocsAI.Application.Abstractions;`

- [ ] **Step 3: Write the endpoint tests**

Create `tests/DevDocsAI.IntegrationTests/RepositoryEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using DevDocsAI.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class RepositoryEndpointsTests(DevDocsApiFactory factory)
{
    private sealed record ConnectionModel(
        Guid Id, string Owner, string Repo, string? Ref, string? CommitSha, string Status, int FileCount);
    private sealed record DocumentModel(Guid Id, string Path, string Status);
    private sealed record SearchHit(string Path);
    private sealed record SearchResponse(string Query, List<SearchHit> Results);

    [Fact]
    public async Task Connect_ingests_supported_files_and_reports_completed()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var connect = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/repository", new { url = "https://github.com/octo/cat" });
        connect.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var connection = await WaitForStatusAsync(client, projectId, "Completed");
        connection.CommitSha.ShouldBe(FakeGitHubRepositoryClient.CommitSha);
        connection.FileCount.ShouldBe(2); // auth.cs + architecture.md; .env and .png skipped

        var docs = await client.GetFromJsonAsync<List<DocumentModel>>(
            $"/api/v1/projects/{projectId}/documents");
        docs!.Select(d => d.Path).ShouldContain("src/auth.cs");
        docs.Select(d => d.Path).ShouldContain("docs/architecture.md");
        docs.ShouldNotContain(d => d.Path == ".env");
    }

    [Fact]
    public async Task Ingested_repo_content_is_searchable()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();
        await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/repository", new { url = "https://github.com/octo/cat" });
        await WaitForStatusAsync(client, projectId, "Completed");
        await WaitUntilDocsProcessedAsync(client, projectId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/search", new { query = "how does authentication work" });
        var body = (await response.Content.ReadFromJsonAsync<SearchResponse>())!;
        body.Results.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Disconnect_removes_the_connection_and_its_documents()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();
        await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/repository", new { url = "https://github.com/octo/cat" });
        await WaitForStatusAsync(client, projectId, "Completed");

        var delete = await client.DeleteAsync($"/api/v1/projects/{projectId}/repository");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"/api/v1/projects/{projectId}/repository");
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var docs = await client.GetFromJsonAsync<List<DocumentModel>>(
            $"/api/v1/projects/{projectId}/documents");
        docs!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Invalid_url_is_rejected()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/repository", new { url = "https://gitlab.com/octo/cat" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Another_user_cannot_read_the_connection()
    {
        var (owner, _, _) = await factory.RegisterAsync();
        var projectId = await owner.CreateProjectAsync();
        await owner.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/repository", new { url = "https://github.com/octo/cat" });

        var (intruder, _, _) = await factory.RegisterAsync();
        var get = await intruder.GetAsync($"/api/v1/projects/{projectId}/repository");
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<ConnectionModel> WaitForStatusAsync(HttpClient client, Guid projectId, string target)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var response = await client.GetAsync($"/api/v1/projects/{projectId}/repository");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var model = (await response.Content.ReadFromJsonAsync<ConnectionModel>())!;
                if (model.Status is "Completed" or "Failed")
                {
                    model.Status.ShouldBe(target);
                    return model;
                }
            }

            await Task.Delay(200);
        }

        throw new Xunit.Sdk.XunitException("Repository connection did not reach a terminal status in time.");
    }

    private static async Task WaitUntilDocsProcessedAsync(HttpClient client, Guid projectId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var docs = await client.GetFromJsonAsync<List<DocumentModel>>(
                $"/api/v1/projects/{projectId}/documents");
            if (docs!.Count > 0 && docs.All(d => d.Status is "Completed" or "Failed"))
            {
                docs.ShouldAllBe(d => d.Status == "Completed");
                return;
            }

            await Task.Delay(200);
        }

        throw new Xunit.Sdk.XunitException("Repository documents were not processed in time.");
    }
}
```

- [ ] **Step 4: Run the repository integration tests**

Run: `dotnet test tests/DevDocsAI.IntegrationTests/DevDocsAI.IntegrationTests.csproj --filter "FullyQualifiedName~RepositoryEndpointsTests"`
Expected: PASS (5 tests). (Docker must be running.)

- [ ] **Step 5: Run the full backend suite (no regressions)**

Run: `dotnet test`
Expected: all pass, 0 warnings. (Upload/document/rag/conversation tests must still pass after the DocumentService refactor.)

- [ ] **Step 6: Commit**
```bash
git add tests/DevDocsAI.IntegrationTests
git commit -m "test(int): repository connect/ingest/search/disconnect + fake GitHub client"
```

---

## Task 11: Frontend — Repository panel

**Files:**
- Modify: `frontend/src/lib/types.ts`
- Create: `frontend/src/components/project-repository.tsx`
- Modify: `frontend/src/app/projects/[id]/page.tsx`

- [ ] **Step 1: Add types**

In `frontend/src/lib/types.ts`, append:
```ts
export interface RepositoryConnection {
  id: string;
  projectId: string;
  provider: string;
  url: string;
  owner: string;
  repo: string;
  ref: string | null;
  commitSha: string | null;
  status: string;
  error: string | null;
  fileCount: number;
  createdAt: string;
  updatedAt: string;
}
```

- [ ] **Step 2: Create the repository panel component**

Create `frontend/src/components/project-repository.tsx`:
```tsx
"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/field";
import { Spinner, StatusDot } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { RepositoryConnection } from "@/lib/types";

const STATUS_TONE: Record<string, "ok" | "warn" | "danger" | "muted"> = {
  Completed: "ok",
  Processing: "warn",
  Pending: "muted",
  Failed: "danger",
};

export function RepositoryPanel({ projectId }: { projectId: string }) {
  const { authFetch } = useAuth();
  const queryClient = useQueryClient();
  const [url, setUrl] = useState("");
  const [error, setError] = useState<string | null>(null);

  const connection = useQuery({
    queryKey: ["repository", projectId],
    queryFn: async () => {
      try {
        return await authFetch<RepositoryConnection>(`/api/v1/projects/${projectId}/repository`);
      } catch (e) {
        if (e instanceof ApiError && e.status === 404) return null;
        throw e;
      }
    },
    refetchInterval: (query) =>
      query.state.data?.status === "Pending" || query.state.data?.status === "Processing" ? 1500 : false,
  });

  const invalidateAll = () => {
    queryClient.invalidateQueries({ queryKey: ["repository", projectId] });
    queryClient.invalidateQueries({ queryKey: ["documents", projectId] });
  };

  const connect = useMutation({
    mutationFn: () =>
      authFetch<RepositoryConnection>(`/api/v1/projects/${projectId}/repository`, {
        method: "POST",
        body: { url: url.trim(), ref: null },
      }),
    onSuccess: () => {
      setUrl("");
      setError(null);
      invalidateAll();
    },
    onError: (e) => setError(e instanceof ApiError ? e.message : "Could not connect the repository."),
  });

  const resync = useMutation({
    mutationFn: () =>
      authFetch(`/api/v1/projects/${projectId}/repository/resync`, { method: "POST" }),
    onSuccess: invalidateAll,
  });

  const disconnect = useMutation({
    mutationFn: () => authFetch(`/api/v1/projects/${projectId}/repository`, { method: "DELETE" }),
    onSuccess: invalidateAll,
  });

  const conn = connection.data;
  const busy = conn?.status === "Pending" || conn?.status === "Processing";

  return (
    <div className="rounded-xl border border-line bg-panel/40 p-6">
      <span className="eyebrow">Repository</span>

      {!conn ? (
        <form
          className="mt-3 flex flex-col gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            if (url.trim()) connect.mutate();
          }}
        >
          <p className="text-xs text-muted">Connect a public GitHub repository to index its files.</p>
          <div className="flex gap-2">
            <Input
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              placeholder="https://github.com/owner/repo"
              aria-label="GitHub repository URL"
            />
            <Button type="submit" size="sm" disabled={connect.isPending || !url.trim()}>
              {connect.isPending ? <Spinner /> : "Connect"}
            </Button>
          </div>
          {error && <p className="text-sm text-danger">{error}</p>}
        </form>
      ) : (
        <div className="mt-3">
          <div className="flex items-center gap-2">
            <StatusDot tone={STATUS_TONE[conn.status] ?? "muted"} pulse={busy} />
            <a
              href={conn.url}
              target="_blank"
              rel="noopener noreferrer"
              className="font-mono text-xs text-ink hover:text-accent"
            >
              {conn.owner}/{conn.repo}
            </a>
            <span className="font-mono text-[0.7rem] text-faint">
              {conn.status}
              {conn.commitSha ? ` · ${conn.commitSha.slice(0, 7)}` : ""}
              {conn.status === "Completed" ? ` · ${conn.fileCount} files` : ""}
            </span>
          </div>
          {conn.status === "Failed" && conn.error && (
            <p className="mt-2 text-xs text-danger">{conn.error}</p>
          )}
          <div className="mt-3 flex gap-2">
            <Button variant="outline" size="sm" disabled={busy || resync.isPending} onClick={() => resync.mutate()}>
              {resync.isPending ? <Spinner /> : "Re-sync"}
            </Button>
            <Button variant="ghost" size="sm" disabled={disconnect.isPending} onClick={() => disconnect.mutate()}>
              Disconnect
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Wire the panel into the Overview aside**

In `frontend/src/app/projects/[id]/page.tsx`, add the import near the other component imports:
```tsx
import { RepositoryPanel } from "@/components/project-repository";
```
Then in the `Overview` component's `<aside>`, add `<RepositoryPanel>` right above `<DocumentsPanel>`:
```tsx
      <aside className="flex flex-col gap-4">
        <RepositoryPanel projectId={project.id} />
        <DocumentsPanel projectId={project.id} />
```

- [ ] **Step 4: Typecheck, lint, build**

Run (from `frontend/`): `npx tsc --noEmit && npm run lint && npm run build`
Expected: tsc exit 0, lint clean, build "Compiled successfully".

- [ ] **Step 5: Commit**
```bash
git add frontend/src/lib/types.ts frontend/src/components/project-repository.tsx frontend/src/app/projects/[id]/page.tsx
git commit -m "feat(web): repository connect panel on the project page"
```

---

## Task 12: Wrap-up — full verification + docs

**Files:**
- Modify: `IMPLEMENTATION_PLAN.md`
- Modify: `memory/devdocs-ai-status.md` (agent memory)

- [ ] **Step 1: Full backend suite + zero-warning build**

Run (from `backend/`): `dotnet build && dotnet test`
Expected: 0 warnings; all unit + integration tests pass.

- [ ] **Step 2: Frontend gates**

Run (from `frontend/`): `npx tsc --noEmit && npm run lint && npm run build`
Expected: all clean.

- [ ] **Step 3: (Optional) live smoke test**

With the stack running (Postgres + API + `npm run dev` + Gemini key + Ollama), connect a small public repo (e.g. a tiny docs repo) in the browser, watch the status reach Completed, then ask a question grounded in it. Confirm citations reference repo file paths.

- [ ] **Step 4: Update the living plan + memory**

Update `IMPLEMENTATION_PLAN.md`'s status header to "Phase 7 complete → next Phase 8 (Agents)" and add a Phase 7 bullet (mirroring the Phase 6 entry style, with the new test count). Update `memory/devdocs-ai-status.md` with the Phase 7 summary + any gotchas discovered.

- [ ] **Step 5: Commit**
```bash
git add IMPLEMENTATION_PLAN.md
git commit -m "docs: mark Phase 7 (GitHub integration) complete"
```

---

## Self-review notes (author)

- **Spec coverage:** validate URL (Task 2) · clone/retrieve via tarball (Tasks 3/8) · ignore unsupported + secrets (Task 4 ingestor + Task 6 pre-filter) · `.gitignore` via tarball (Task 6, documented) · process + embed (reuses existing `DocumentProcessor`, exercised in Task 10) · store metadata (Tasks 1/5) · no secrets stored (no token anywhere) · behind an abstraction (`IGitHubRepositoryClient`, Task 3). Milestone "connect a public repo and query it" is asserted end-to-end in Task 10 (`Ingested_repo_content_is_searchable`).
- **Type consistency:** `IngestOutcome.Accepted/Rejected`, `IDocumentIngestor.IngestAsync(...ISet<string> seenHashes...)`, `RepositoryConnection.MarkCompleted(commitSha, fileCount)`, `RepositoryArchive(CommitSha, Content)`, `IRepositoryIngestor.IngestAsync(connectionId, ct)`, and `GitHubRepoRef(Owner, Repo, Ref)` are used identically across tasks.
- **No placeholders:** every step has concrete code/commands.
- **Known follow-ups (not in scope):** dashboard project-card "indexed" badge (separate); private repos/tokens; webhooks.
