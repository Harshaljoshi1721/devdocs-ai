# Phase 7 — GitHub Integration — Design

> Date: 2026-07-28 · Status: **approved, pre-implementation**
> Spec source of truth: [`PROJECT_SPEC.md`](../../../PROJECT_SPEC.md) § "GitHub Repository Integration" + "Phase 7".
> Living plan: [`IMPLEMENTATION_PLAN.md`](../../../IMPLEMENTATION_PLAN.md).

## 1. Goal & scope

Let a user connect a **public GitHub repository** to a project, ingest its supported files
through the existing Phase 3–5 pipeline (screen → store → `Document` → chunk → embed → index),
and then chat/search over it exactly as with uploaded files.

**Milestone (from spec):** connect a public repo and query it.

**In scope**
- Validate a GitHub repo URL (public, HTTPS).
- Download the repo contents as a tarball and walk it.
- Ignore unsupported files (existing extension allowlist) and secret files (existing denylist).
- Respect `.gitignore` "where possible" (see §5).
- Reuse the existing ingestion pipeline to produce `Document`s + chunks + embeddings.
- Track connection + processing status; allow manual **re-sync** and **disconnect**.
- Frontend "Connect repository" panel on the project Overview tab.

**Out of scope (this phase)**
- Private repos / auth tokens (spec: "must not store secrets").
- Webhooks / automatic sync on push.
- Non-GitHub providers (the abstraction leaves room; only GitHub is implemented).
- Incremental/diff sync (re-sync re-ingests a fresh snapshot).

## 2. Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| Fetch | **Download tarball via HTTP** (`codeload.github.com/{owner}/{repo}/tar.gz/{ref}`) | No git binary / native library; one request; matches the existing typed-`HttpClient` adapter pattern. `System.Formats.Tar` + `GZipStream` are built into .NET 10 — **no new NuGet dependency**. |
| Access | **Public repos only, no token** | Matches the milestone and the "must not store secrets" rule. Unauthenticated GitHub allows enough for a single tarball download. |
| Sync | **One-time snapshot + manual re-sync** | Ingest at the resolved commit on connect; user can re-sync on demand (re-ingests a fresh snapshot). |
| Cardinality | **One `RepositoryConnection` per project** (unique on `ProjectId`); re-connecting replaces it | Simple UX + schema. Uploaded files and repo files coexist as `Document`s. |
| Doc linkage | `Document` gains a nullable **`RepositoryConnectionId`** FK | Lets re-sync/disconnect delete exactly the repo-sourced documents; manual uploads keep it null. |
| `.gitignore` | **Rely on the tarball (tracked files only) + secret denylist**; do not parse `.gitignore` | GitHub's tarball is a `git archive` and already excludes untracked/ignored files. This is the "where possible" interpretation. |
| Limits | **max 1,500 files, max 25 MB total (decompressed), 5 MB per file** (per-file reuses the existing upload cap) | Abuse / tar-bomb guard at portfolio scale. |

## 3. Architecture overview

```
POST /projects/{id}/repository  ──►  RepositoryConnectionService
                                      (validate URL, create connection=Pending,
                                       enqueue job)  ──► 202 + connection
                                             │
                              IBackgroundTaskQueue (existing)
                                             │
                                             ▼
                         RepositoryIngestionJob (new)
   status→Processing
   ├─ IGitHubRepositoryClient.DownloadTarballAsync(owner,repo,ref) ─► (commitSha, Stream)
   ├─ walk tar.gz entries:
   │    strip "{repo}-{sha}/" prefix → repo-relative path
   │    IFileFilter (existing allowlist + secret denylist)
   │    enforce RepoIngestionOptions caps
   │    DocumentService.IngestFileAsync(projectId, path, bytes, connectionId)  ◄─ shared helper
   │        (hash + dedupe + store + create Document + enqueue DocumentProcessor)
   └─ status→Completed (+commitSha,+fileCount)  |  Failed (+error)
```

Everything GitHub-specific is behind **`IGitHubRepositoryClient`** (Application port,
Infrastructure adapter), consistent with the AI/vector/storage ports.

## 4. Data model

### New entity `RepositoryConnection : Entity`
| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid (v7) | app-assigned, `ValueGeneratedNever()` (see Phase 6 gotcha) |
| `ProjectId` | Guid | FK → projects, cascade delete; **unique** |
| `Provider` | enum `RepositoryProvider { GitHub }` | future-proof |
| `Owner`, `Repo` | string | parsed from URL |
| `Url` | string | normalized `https://github.com/{owner}/{repo}` |
| `Ref` | string? | requested branch/tag; null = default branch |
| `CommitSha` | string? | resolved commit actually ingested |
| `Status` | enum `ProcessingStatus` (reused) | Pending → Processing → Completed/Failed |
| `Error` | string? | failure detail |
| `FileCount` | int | supported files ingested |
| `CreatedAt`/`UpdatedAt` | DateTime | audit (existing convention) |

Methods mirror `Document`: `MarkProcessing()`, `MarkCompleted(commitSha, fileCount)`, `MarkFailed(error)`, `Reset()` (for re-sync back to Pending).

### `Document` change
Add nullable `RepositoryConnectionId` (Guid?) + FK → `repository_connections` (set null / no cascade to avoid multiple cascade paths; the job deletes repo docs explicitly on re-sync/disconnect). Null = manual upload.

### Migration
`AddRepositoryConnections`: create `repository_connections` (unique index on `ProjectId`, index on `Status`), add `RepositoryConnectionId` column + index to `documents`.

## 5. `.gitignore` handling

GitHub's tarball endpoint returns a `git archive` of the ref — it contains **only files tracked
in that commit**. Untracked / `.gitignore`d files (e.g. `node_modules/`, build output, an
uncommitted `.env`) are therefore already absent. The existing secret denylist additionally
rejects any committed `.env`, keys, and certs. We do **not** parse `.gitignore` to further
exclude committed-but-ignored files — an uncommon case, and the dangerous subset (secrets) is
already covered. This satisfies the spec's "respect `.gitignore` **where possible**." Documented
as a known limitation.

## 6. Ports & adapters

### `IGitHubRepositoryClient` (Application/Abstractions)
```csharp
public sealed record RepositoryArchive(string CommitSha, Stream Content); // Content = tar.gz stream
public interface IGitHubRepositoryClient
{
    Task<RepositoryArchive> DownloadTarballAsync(string owner, string repo, string? @ref, CancellationToken ct);
}
```
- Adapter `GitHubRepositoryClient` (Infrastructure) via typed `HttpClient`.
- Resolves the commit SHA from the `Content-Disposition`/redirect (`{repo}-{sha}.tar.gz`) or a
  lightweight `GET /repos/{owner}/{repo}/commits/{ref}` head call; ref defaults to the repo's
  default branch (tarball endpoint accepts an empty ref → default).
- Clear errors for 404 (repo not found / private) and rate-limit (403).
- `GitHubOptions` (base URLs, timeout); no key.

### URL validation (Application)
`GitHubUrlParser.TryParse(url) → (owner, repo, ref?)`:
- Accept only `https://github.com/{owner}/{repo}` (optional `.git`, optional `/tree/{ref}`).
- Reject other hosts, `git@`/SSH, IPs, `localhost`, embedded credentials, path traversal.
- Invalid → `ValidationException` (400).

## 7. Ingestion pipeline reuse

Refactor the per-file work in `DocumentService.UploadAsync` into a shared private helper and
expose an ingestion entry point used by the job:
```csharp
Task<IngestOutcome> IngestFileAsync(
    Guid projectId, string relativePath, Stream content, long length,
    Guid? repositoryConnectionId, CancellationToken ct);
```
The helper keeps the existing behaviour: `IFileFilter` screen → SHA-256 hash → content-hash
dedupe (per project) → `IFileStorage` store → create `Document` (Path = repo-relative path,
Name = basename, `RepositoryConnectionId` set) → enqueue `DocumentProcessor`. `UploadAsync`
becomes a thin wrapper over the same helper (connectionId = null). This guarantees uploads and
repo files travel identical logic.

`RepositoryIngestionJob` (Application/Features/Repositories):
1. load connection; `MarkProcessing()`.
2. `DownloadTarballAsync`; on network/404/403 → `MarkFailed`.
3. `GZipStream` → `TarReader`; for each **file** entry:
   - strip leading `{repo}-{sha}/` path segment; skip directories, symlinks, and `..` paths.
   - enforce caps (per-file size, running total bytes, running file count) — abort with a clear
     error if exceeded (tar-bomb guard).
   - copy entry to a bounded buffer/temp stream; `IngestFileAsync(...)`. (Filter rejections and
     dedupes are counted but not fatal.)
4. `MarkCompleted(commitSha, fileCount)`; save.

Re-sync: `Reset()` connection → delete `Document`s where `RepositoryConnectionId == id`
(chunks/embeddings/vectors cascade) → enqueue the same job. Disconnect: delete those docs +
remove the connection.

## 8. API (owner-scoped, `[Authorize]`)

`RepositoryController` under `/api/v1/projects/{projectId}/repository`:
| Verb | Path | Body | Result |
|------|------|------|--------|
| POST | `/` | `{ url, ref? }` | 202 `ConnectionResponse` (Pending); enqueues ingestion. Replaces any existing connection. |
| GET  | `/` | — | `ConnectionResponse` or 404 if none |
| POST | `/resync` | — | 202; re-ingests |
| DELETE | `/` | — | 204; removes connection + repo docs |

`ConnectionResponse(Id, Url, Owner, Repo, Ref, CommitSha, Status, Error, FileCount, CreatedAt, UpdatedAt)`.
Ownership enforced via the project-owner check used everywhere else; cross-tenant → 404.

## 9. Frontend

`components/project-repository.tsx` — a "Repository" card in the Overview tab (beside Documents):
- No connection: URL input (+ optional branch) + **Connect**.
- Connected: shows `owner/repo`, branch, short commit SHA, file count, and a status pill
  (Pending/Indexing/Completed/Failed, **polling** while in progress via the existing
  `refetchInterval` pattern) with **Re-sync** and **Disconnect** actions.
- Ingested files appear in the existing Documents list; the `IndexStatus` header badge already
  reflects the combined total. Types added to `lib/types.ts`.

## 10. Configuration

`RepoIngestionOptions` (section `RepoIngestion`): `MaxFiles=1500`, `MaxTotalBytes=26_214_400`
(25 MB), `MaxFileBytes` defaults to the existing `UploadOptions.MaxFileSizeBytes` (5 MB).
`GitHubOptions` (section `GitHub`): `ApiBaseUrl`, `CodeloadBaseUrl`, `TimeoutSeconds`. Validated
via the options pattern; no secrets.

## 11. Security

- **SSRF guard:** URL allowlist (github.com only), no IP/localhost/credentials; the client only
  ever calls the two fixed GitHub hosts.
- **Secrets:** existing denylist applied per file; no tokens stored; nothing sensitive in logs.
- **Tar-bomb / zip-bomb:** enforce total-bytes and file-count caps while streaming; abort early.
- **Path traversal:** reject `..`/absolute entry paths; rely on `LocalFileStorage` path safety.
- **Prompt injection:** unchanged — ingested content is treated as data, never instructions.

## 12. Testing

- **Fake `IGitHubRepositoryClient`** returning an in-memory `tar.gz` fixture (no network in CI,
  same approach as the fake AI providers).
- **Unit:** URL parse/validate (accept/reject matrix); tar walk (prefix strip, directory/symlink
  skip, `..` reject, allowlist/denylist, caps → abort); `IngestFileAsync` dedupe; re-sync deletes
  prior repo docs; job status transitions on success/failure.
- **Integration (Testcontainers):** POST connect → poll status→Completed → documents appear →
  `/search` + a chat `/ask` return grounded answers over repo content; cross-tenant connect/get
  → 404; disconnect removes docs; malformed URL → 400.

## 13. Rough change checklist

- Domain: `RepositoryConnection` entity, `RepositoryProvider` enum, `Document.RepositoryConnectionId`.
- Application: `IGitHubRepositoryClient` port + `RepositoryArchive`; `GitHubUrlParser`;
  `IRepositoryConnectionRepository`; `RepositoryConnectionService`; `RepositoryIngestionJob`;
  `DocumentService.IngestFileAsync` refactor; `RepoIngestionOptions`; DTOs.
- Infrastructure: `GitHubRepositoryClient` (typed HttpClient) + `GitHubOptions`; EF config +
  `AddRepositoryConnections` migration; repository impl; DI wiring.
- Api: `RepositoryController`.
- Frontend: `project-repository.tsx`, types, Overview wiring.
- Tests: fake client + fixtures; unit + integration per §12.
