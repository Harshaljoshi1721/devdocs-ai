# DevDocs AI — Implementation Plan

> Status: **Phase 7 (GitHub integration) complete** — next up is Phase 8 (Agents & tools). Decisions D1–D3 resolved (§15). **A live run needs one free Gemini key** (`Gemini:ApiKey`) + Ollama running; tests use fakes and pass without either.
> Done so far:
> - **Phase 1** (Foundation).
> - **Phase 2** (EF Core + Postgres/pgvector, JWT auth with rotating refresh tokens, Project CRUD with per-user isolation).
> - **Frontend pulled forward from Phase 6** — auth (register/login) + dashboard (create/delete projects) + project detail (edit/delete, placeholder Chat/Search/Agents/Docs tabs), wired to the API, verified in a browser. Dark technical-editorial design system (Fraunces/IBM Plex/JetBrains Mono, lime accent).
> - **Phase 3** (File Ingestion): `Document` entity + `ProcessingStatus`/`FileType` enums; `IFileFilter` (extension allowlist + secret denylist for `.env`/keys/certs); `IFileStorage` → `LocalFileStorage` (path-traversal-safe, SHA-256 + size); in-process `IBackgroundTaskQueue` + `QueuedHostedService` (ready for Phase 4's processor); `DocumentService` (upload with ownership check, per-file screening, content-hash dedupe, list, delete); `DocumentsController` (`/api/v1/projects/{projectId}/documents` multipart upload / list / delete); EF `AddDocuments` migration; `UploadOptions`/`StorageOptions`. Documents land at **Pending** (processing pipeline is Phase 4).
>
> - **Phase 4** (Text Processing): `DocumentChunk` entity (Content, ChunkIndex, StartLine/EndLine, nullable EmbeddingReference); `ITextChunker` → `LineAwareChunker` (CRLF/CR→LF line-preserving normalize, char-budget chunks, configurable line overlap, oversized-line handling) + `ChunkingOptions`; `IDocumentChunkRepository` + EF impl; `DocumentProcessor` (reads storage → normalizes → chunks → persists; Pending→Processing→Completed/Failed) enqueued on `IBackgroundTaskQueue` from `DocumentService` after upload; `AddDocumentChunks` migration. Upload now kicks off async processing; documents reach **Completed** with chunks carrying 1-based line ranges.
>
> - **Phase 5** (RAG — Google Gemini free tier): AI ports `IEmbeddingService` / `IChatCompletionService` / `IVectorStore` / `IReranker`; Gemini adapters (`GeminiEmbeddingService` `text-embedding-004` 768d batched; `GeminiChatCompletionService` `gemini-2.0-flash`; both via typed `HttpClient`, key from `Gemini:ApiKey`, fail clearly if unset); `PgVectorStore` (pgvector `vector(768)` on `chunk_embeddings`, HNSW cosine index, Infrastructure-only `ChunkEmbeddingRecord` so the vector type never enters Domain); `DocumentProcessor` now embeds chunks + upserts vectors after chunking; `RagService` = embed query → project-scoped cosine search → pass-through rerank → hydrate citations (path + line range) → grounded answer (system prompt refuses when context insufficient); `RagController` `POST /api/v1/projects/{id}/search` + `/ask`; `AddChunkEmbeddings` migration. Tests use deterministic fake embedding/chat providers (no key/network) swapped in via `ConfigureTestServices`.
>
> - **Phase 6** (AI Chat): `Conversation` (aggregate root) + `Message` entities, `MessageCitation` value object, `MessageRole` enum; citations persisted as a `jsonb` column via EF `OwnsMany(...).ToJson()`; `IConversationRepository` + impl; `AddConversations` migration (both Ids `ValueGeneratedNever()` — app-assigned UUID v7, else EF UPDATEs instead of INSERTs child messages). Retrieval extracted from `RagService` into a shared `RetrievalService`/`GroundedChat` reused by chat; `ChatService` (create/list/get/delete conversation + send + **stream**) — history-aware grounded answers, persists Q + A + citations, names the conversation from the first question, project/conversation ownership checks. **SSE streaming**: `IChatCompletionService.StreamAsync` (Ollama NDJSON + Gemini `streamGenerateContent?alt=sse`); `ConversationsController` with an SSE `messages/stream` endpoint (auth/validation fail before the stream commits, so they still return ProblemDetails). **Frontend**: project page Chat tab (conversation list + live token-streamed thread via `fetch`+`ReadableStream`, dependency-free Markdown renderer, source citation chips), Search tab (`/search`), and a Documents upload panel (multipart upload + status polling + delete) in Overview. Streaming uses a new `authRaw` (bearer + one refresh) on the auth context; `readSse` parses the event stream.
>
> - **Phase 7** (GitHub integration): connect a **public** GitHub repo to a project and ingest it through the existing pipeline. `RepositoryConnection` aggregate (`RepositoryProvider` enum, status reuses `ProcessingStatus`, one-per-project unique index) + nullable `Document.RepositoryConnectionId`; `AddRepositoryConnections` migration. Fetch = **tarball download** (`codeload …/tar.gz/{sha}`) via typed `HttpClient` `GitHubRepositoryClient` behind `IGitHubRepositoryClient` — **no new NuGet dep** (`System.Formats.Tar` + `GZipStream` built in); commit resolved first via `api.github.com/…/commits/{ref|HEAD}`. `GitHubUrlParser` (github.com-only allowlist, rejects SSH/creds/traversal → SSRF guard). Per-file ingestion extracted from `DocumentService` into shared **`DocumentIngestor`** (screen→store→dedupe→create `Document`), reused by uploads + repo import. `RepositoryIngestor` background job: strip `{repo}-{sha}/` prefix → `IFileFilter` allowlist/secret denylist → caps (`RepoIngestionOptions`: 1500 files / 25 MB total / 5 MB per file, tar-bomb guard) → enqueue `DocumentProcessor`; marks connection Completed(+commitSha,+fileCount)/Failed. `.gitignore` respected implicitly (tarball = tracked files only). `RepositoryConnectionService` (connect/get/resync/disconnect, replaces existing, ownership-checked) + `RepositoryController` `/api/v1/projects/{id}/repository`. **Frontend**: Repository panel on Overview (connect URL, live status polling, commit/file-count, re-sync/disconnect); repo files show in the Documents list. Fake `IGitHubRepositoryClient` (in-memory tar.gz) for network-free integration tests. Design + plan: `docs/superpowers/{specs,plans}/2026-07-28-github-integration*`.
>
> **Test count: 139 passing** (107 unit + 32 Testcontainers integration), backend build 0 warnings / 0 errors. Frontend typecheck + lint + production build clean.
>
> **To run Phase 5 live:** add a free Gemini key — `cd backend/src/DevDocsAI.Api && dotnet user-secrets set "Gemini:ApiKey" "<key>"` (or `Gemini__ApiKey` in `.env` for Docker). Without it, uploads fail at the embedding step (document → Failed); search/ask return a clear "key not configured" error.
>
> **Next session → Phase 8 (Agents & tools):** agent abstraction + orchestration loop, tool registry with schemas/validation/logging, the four agents (Code Explorer, Documentation Generator, Bug Analysis, Architecture Analyst), `ToolExecution` observability. *Milestone: the four agents usable end-to-end.* (Phase 7 shipped public-GitHub-repo connect + tarball ingestion + the Repository UI — see the Phase 7 bullet above.)
> Source of truth: [`PROJECT_SPEC.md`](PROJECT_SPEC.md). This document does not replace the spec; it interprets it and records the engineering decisions the spec leaves open.

---

## 0. How to read this document

- **Section 1** — my understanding of the product and where the spec is silent/ambiguous.
- **Sections 2–13** — the concrete architecture and technology decisions.
- **Section 14** — phased delivery plan and the exact build order.
- **Section 15** — decisions that need your explicit approval before I start.
- **Section 16** — risks and trade-offs.
- **Appendix A** — every "the spec was silent, I decided X" call in one table.

---

## 1. Understanding of the project

DevDocs AI is a **SaaS-style codebase-intelligence platform**. A developer signs up, creates a project, uploads source/docs (or later connects a GitHub repo), the system ingests and indexes the content, and the developer then asks natural-language questions and gets **grounded, citation-backed answers** plus specialised AI agents (code explorer, doc generator, bug analyst, architecture analyst) that can call tools.

The spec is unusually complete on **product scope** and **layering discipline** (clean architecture, Domain must not depend on infrastructure/providers, everything external behind interfaces). It is deliberately open on **concrete provider/infra choices** ("evaluate during implementation rather than hardcode prematurely").

The non-negotiables I extracted:

1. **Clean, layered backend** — `Domain → Application → Infrastructure → Api`, dependencies point inward, no leakage of EF Core / ASP.NET / LLM / vector-DB types into Domain or Application.
2. **Grounded RAG** — answers cite source files (and line ranges where possible); the assistant must say "not found in project context" rather than hallucinate.
3. **Provider abstraction** — LLM, embeddings, vector storage, file storage, and GitHub all sit behind interfaces so providers can be swapped.
4. **Multi-tenancy security** — a user can never read another user's project. Auth + per-resource authorization is mandatory.
5. **Background processing** — ingestion/chunking/embedding must not block HTTP; processing status is observable (Pending/Processing/Completed/Failed).
6. **Production posture** — validation, error handling, structured logging, tests (unit + integration + AI eval), Docker, CI/CD, no committed secrets.
7. **Incremental delivery** — build in the 9 phases the spec defines; do not generate the whole app at once.

### 1.1 Contradictions / ambiguities / gaps found in the spec

| # | Gap / ambiguity | Resolution |
|---|---|---|
| A | **Embeddings provider unspecified — and Anthropic (Claude) offers no embeddings API.** The spec wants an "LLM provider abstraction" but a working RAG pipeline needs a *separate* embeddings source. | Decision **required from you** — see §15-D1. Chat and embeddings are decoupled behind two interfaces so the choice is late-bound. |
| B | Vector storage: "prefer easy-to-run-locally, Postgres-compatible where practical, behind an interface." | Decided: **pgvector** (Postgres extension) behind `IVectorStore`. §5. |
| C | Auth: `User` has "PasswordHash **or** external auth identifier" — leaves open local vs OAuth/social. | Decided: **local email+password with JWT access + rotating refresh tokens** for MVP; external providers deferred behind the same abstraction. §9. Flagged in §15-D2. |
| D | "Commands / Queries" implies CQRS but no mediator library named. | Decided: lightweight use-case handler classes, **no MediatR** (recently commercial-licensed). §4. |
| E | Streaming responses "if supported / if practical." | Decided: **SSE** streaming for chat; degrade gracefully to non-streaming. §7, §8. |
| F | Project sharing: `Project members` table + "another user's projects" implies collaboration, but core UX is single-owner. | Decided: model `ProjectMember` from day one (owner role) but ship **owner-only** access first; invite/roles are a later phase. §5, §16. |
| G | Line-number citations "where possible" — not all chunkers preserve exact lines after normalisation. | Decided: chunker preserves `StartLine`/`EndLine` from the *original* file; normalisation is line-preserving. §6. |
| H | Reranking "optionally." | Decided: `IReranker` interface with a **no-op/​heuristic default**; pluggable model reranker later. §6. |
| I | Config/secrets strategy unstated beyond "use env vars." | Decided: env vars + `.env` (gitignored) locally, `appsettings` for non-secret defaults, options pattern + validation. §10. |

None of these are true blockers except **A (embeddings/provider + API keys)**, which needs your input because it costs money and needs credentials I cannot create.

---

## 2. System architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Browser (user)                              │
└───────────────▲─────────────────────────────────────────────────────┘
                │ HTTPS (JSON REST + SSE for chat streaming)
┌───────────────┴─────────────────────────────────────────────────────┐
│  Frontend — Next.js (App Router) / React / TypeScript / Tailwind     │
│  Auth UI · Dashboard · Project · Chat · Search · Agents · Docs · Err │
└───────────────▲─────────────────────────────────────────────────────┘
                │
┌───────────────┴─────────────────────────────────────────────────────┐
│  Backend — ASP.NET Core Web API (DevDocsAI.Api)                      │
│  Controllers /api/v1 · Auth (JWT) · Middleware · Exception handling  │
│  OpenAPI · Rate limiting · Structured logging (Serilog)              │
├──────────────────────────────────────────────────────────────────────┤
│  Application  — use cases, DTOs, validation, port interfaces          │
│  Domain       — entities, value objects, enums, domain rules          │
├──────────────────────────────────────────────────────────────────────┤
│  Infrastructure — EF Core, LLM/embeddings adapters, pgvector store,   │
│  file storage, GitHub client, background jobs                         │
└───────┬───────────────────┬────────────────────┬────────────────────┘
        │                   │                    │
   ┌────▼─────┐      ┌──────▼───────┐     ┌──────▼────────┐
   │PostgreSQL│      │ LLM provider │     │ Embeddings    │
   │+ pgvector│      │ (chat/agents)│     │ provider      │
   └──────────┘      └──────────────┘     └───────────────┘
        ▲
   ┌────┴───────────────────────┐
   │ Local file storage (disk); │
   │ swappable to S3/blob later │
   └────────────────────────────┘
```

**Ingestion (async) flow:** upload/connect → enqueue job → discover files → filter (extension allowlist + secret denylist) → read → normalise → chunk (line-aware) → embed → store vectors + chunk metadata → mark Document `Completed`. Status polled by the frontend.

**Query (RAG) flow:** user question → embed query → vector search (top-k, project-scoped) → optional rerank → build grounded context → LLM answer with a "cite sources / say if unknown" system prompt → return answer + citations. Agents wrap this with tool-calling loops.

---

## 3. Technology stack & justification

### Backend
| Concern | Choice | Why |
|---|---|---|
| Runtime/SDK | **.NET 10 (LTS)** — installed: `10.0.302` | Latest LTS; already on this machine; matches spec's "keep compatible with dev environment." |
| Web | **ASP.NET Core Web API, Controllers + `Asp.Versioning`** | Spec asks for versioned REST (`/api/v1/...`); controllers give clear, discoverable structure for a portfolio. |
| ORM | **EF Core 10 + Npgsql** | Spec mandates EF Core + PostgreSQL. |
| Vector | **pgvector** via `pgvector` + `pgvector-dotnet` | One datastore, trivial local Docker, Postgres-native; behind `IVectorStore`. |
| Validation | **FluentValidation** | Clean, testable request validation in Application layer. |
| Mapping | **Manual mapping / Mapster** | Avoids AutoMapper's new commercial licensing; explicit and fast. |
| Auth | **JWT (access + refresh) + BCrypt** (`Microsoft.AspNetCore.Authentication.JwtBearer`, `BCrypt.Net-Next`) | Self-contained, demonstrable, no external IdP dependency for MVP. |
| Background jobs | **Abstraction `IBackgroundJobQueue`**; in-process `Channel`+`BackgroundService` first, **Hangfire (Postgres storage)** when durability matters (Phase 5+) | Start simple, upgrade behind the same port with minimal churn; Hangfire persists long embedding jobs across restarts and gives a dashboard. |
| Logging | **Serilog** (console + structured) | Spec requires structured logging. |
| LLM/embeddings client | Provider SDK(s) behind our own ports; may sit on **`Microsoft.Extensions.AI`** abstractions internally | Keeps Domain/Application provider-agnostic; MEAI gives a clean `IChatClient`/`IEmbeddingGenerator` substrate. |
| Testing | **xUnit + Shouldly + NSubstitute + Testcontainers + `WebApplicationFactory`** | Unit + real-Postgres integration tests as the spec requires. (Shouldly over FluentAssertions — the latter went commercial in v8.) |

### Frontend
| Concern | Choice | Why |
|---|---|---|
| Framework | **Next.js 15 (App Router) / React 19 / TypeScript** — Node `v22.17` installed | Spec mandate; modern, hireable stack. |
| Styling | **Tailwind CSS v4** | Spec mandate. |
| Server state | **TanStack Query** | Caching, polling processing status, mutations — fits the RAG dashboard well. |
| Client state | Minimal **React Context** (auth/session); add Zustand only if needed | Avoid premature complexity. |
| Forms/validation | **React Hook Form + Zod** | Type-safe forms mirroring backend validation. |
| Markdown/citations | **react-markdown + shiki/highlight** | Render answers, code, and source excerpts. |
| Chat streaming | **SSE / fetch streaming** | Token streaming when the provider supports it. |
| Testing | **Vitest + Testing Library**; Playwright (later) | Component + a few e2e flows. |

### Ops
Docker + docker-compose (Postgres+pgvector, API, web), GitHub Actions CI (build, test, lint, docker build). Deferred deploy targets discussed in §13.

**Dependency discipline:** every library above maps to an explicit spec requirement. No speculative additions; new deps get justified in the PR that introduces them.

---

## 4. Repository structure

Monorepo (single repo, clear top-level split):

```
DevDocs-AI/
├─ PROJECT_SPEC.md              # source of truth (never edited)
├─ IMPLEMENTATION_PLAN.md       # this file
├─ README.md
├─ .gitignore  .editorconfig  .env.example
├─ docker-compose.yml
├─ .github/workflows/ci.yml
│
├─ backend/
│  ├─ DevDocsAI.sln
│  ├─ src/
│  │  ├─ DevDocsAI.Domain/          # entities, VOs, enums, domain exceptions — zero external deps
│  │  ├─ DevDocsAI.Application/     # use cases, DTOs, validators, PORT interfaces
│  │  ├─ DevDocsAI.Infrastructure/  # EF Core, pgvector, LLM/embeddings adapters, storage, GitHub, jobs
│  │  └─ DevDocsAI.Api/             # controllers, DI, middleware, auth, OpenAPI, config
│  └─ tests/
│     ├─ DevDocsAI.UnitTests/
│     └─ DevDocsAI.IntegrationTests/
│
└─ frontend/
   ├─ app/ (App Router)  components/  lib/ (api client, auth)  hooks/  types/
   ├─ package.json  tsconfig.json  tailwind.config
   └─ __tests__/
```

Dependency rule (enforced by project references + a solution-level check later): `Api → Application → Domain`, `Infrastructure → Application/Domain`, `Api → Infrastructure` for DI wiring only. Application defines interfaces; Infrastructure implements them.

---

## 5. Core modules & responsibilities

- **Domain** — `User`, `Project`, `ProjectMember`, `Document`, `SourceFile`, `DocumentChunk`, `Conversation`, `Message`, `Agent`, `ToolExecution`, `ProcessingJob`, `UsageRecord`; enums (`ProcessingStatus`, `MessageRole`, `AgentType`, `FileType`); value objects (e.g. `LineRange`, `ContentHash`); domain rules (a chunk always belongs to a document that belongs to a project; secret files never become documents).
- **Application** — use cases: `CreateProject`, `GetProject`, `ListProjects`, `UpdateProject`, `DeleteProject`, `UploadDocument`, `ProcessDocument`, `AskQuestion`, `SearchProject`, `GenerateDocumentation`, `AnalyseError`, `RunAgent`. Ports: `IChatCompletionService`, `IEmbeddingService`, `IReranker`, `IVectorStore`, `IFileStorage`, `IGitHubRepositoryClient`, `IBackgroundJobQueue`, `ITextChunker`, `IFileFilter`, repositories, `IUnitOfWork`.
- **Infrastructure** — `AppDbContext` + configurations + migrations; `PgVectorStore`; chat/embeddings adapters; `LocalFileStorage`; `GitHubRepositoryClient`; chunker + filter; background job runner (Channel → Hangfire).
- **Api** — versioned controllers per resource, JWT auth + authorization policies (owner/member), global exception→ProblemDetails middleware, rate limiting, OpenAPI/Swagger, SSE chat endpoint, health checks.

---

## 6. Data model & database design (initial)

Tables (PostgreSQL, snake_case, `uuid` PKs, `created_at`/`updated_at`):

- **users**(id, email ✦unique, name, password_hash, created_at, updated_at)
- **projects**(id, name, description, owner_id→users, created_at, updated_at)
- **project_members**(id, project_id→projects, user_id→users, role, created_at) — unique(project_id,user_id)
- **documents**(id, project_id→projects, name, path, file_type, content_hash, size, processing_status, error?, created_at, updated_at)
- **source_files** — stored file location/metadata (kept distinct from logical Document; supports future re-processing)
- **document_chunks**(id, document_id→documents, content, chunk_index, start_line, end_line, embedding_ref, created_at)
- **chunk_embeddings**(chunk_id→document_chunks, embedding `vector(N)`) — **pgvector**; `N` fixed by chosen embedding model; HNSW/IVFFlat index for cosine similarity
- **conversations**(id, project_id, user_id, title, created_at, updated_at)
- **messages**(id, conversation_id→conversations, role, content, created_at) — plus citations JSON (file path, line range, excerpt)
- **agents**(id, project_id, name, description, system_instructions, agent_type, created_at, updated_at)
- **tool_executions**(id, message_id?/conversation_id, tool_name, input_json, output_json, status, error?, duration_ms, created_at) — observability of tool calls
- **processing_jobs**(id, project_id, document_id?, type, status, attempts, error?, created_at, updated_at)
- **usage_records**(id, user_id, project_id?, kind, tokens_in, tokens_out, cost_estimate?, created_at)

Indexes: FK indexes, `documents(project_id,processing_status)`, vector index on `chunk_embeddings.embedding`. Migrations via EF Core; pgvector enabled by a migration that runs `CREATE EXTENSION IF NOT EXISTS vector`.

**Vector store abstraction:** `IVectorStore` (Upsert(chunkId, vector, metadata), Query(projectId, queryVector, k) → scored chunk refs, DeleteByDocument). Default impl = pgvector; the interface keeps a dedicated vector DB (Qdrant/Pinecone) swappable later.

---

## 7. AI / LLM architecture

- **Ports (Application):** `IChatCompletionService` (messages in → assistant message / stream, with optional tool definitions + tool-call results), `IEmbeddingService` (texts → vectors, exposes model + dimension), `IReranker` (query + candidates → reordered), and a `ToolRegistry`.
- **Adapters (Infrastructure):** implemented against the chosen provider SDK(s); may internally use `Microsoft.Extensions.AI` `IChatClient`/`IEmbeddingGenerator`. Model IDs, keys, and endpoints come from config, never hardcoded.
- **Agents:** each agent = system instructions + allowed tool set + an orchestration loop (call model → if tool_use, execute tool via registry, log `ToolExecution`, feed result back → repeat until final). Agent types: Code Explorer, Documentation Generator, Bug Analysis, Architecture Analyst.
- **Tools:** `SearchProject`, `SearchFiles`, `ReadFile`, `FindReferences`, `GetProjectStructure`, `GetDocument`, `GenerateDocumentation` — each with typed input/output schema, validation, logging, and error handling. All tool executions are recorded (observable).
- **Groundedness:** system prompt enforces "answer only from provided context; if insufficient, say so"; retrieved chunks carry file path + line range; the response includes structured citations. Bug agent explicitly separates *codebase evidence* from *AI hypothesis*.

**Model selection (recommendation, pending §15-D1):** default chat/agents to a current Claude model (e.g. Claude Sonnet for cost/quality, Claude Opus for the hardest agent tasks) if you go Anthropic; embeddings from OpenAI/Voyage/local since Anthropic has none. All model IDs are config values.

---

## 8. RAG / retrieval architecture

Pipeline stages (each a small, unit-testable component):

1. **Discover** — walk uploaded set / repo tree.
2. **Filter** — extension allowlist (spec's code/doc/config lists) + secret denylist (`.env`, `.env.*`, keys, certs, secrets). `IFileFilter`.
3. **Read + normalise** — decode text, normalise line endings *without* losing line numbers.
4. **Chunk** — line-aware windowed chunking (size + overlap tuned per file type; code chunked with structural awareness where cheap). Preserves `StartLine`/`EndLine`. `ITextChunker`.
5. **Embed** — batch embed chunks via `IEmbeddingService`.
6. **Store** — vectors + metadata via `IVectorStore`; chunk rows in Postgres.
7. **Retrieve** — embed query → top-k cosine search scoped to project → optional rerank.
8. **Assemble context** — token-budgeted context builder with citations.
9. **Generate** — grounded LLM answer + sources.

Retrieval is always **project-scoped** at the query level (defence in depth against cross-tenant leakage) and again enforced by authorization at the API layer.

---

## 9. Authentication & authorization

- **AuthN:** local register/login; passwords hashed with BCrypt; short-lived **JWT access token** + longer-lived **rotating refresh token** persisted (hashed) in DB and revocable. Refresh token delivered as an **httpOnly, Secure cookie**; access token held in memory on the client. Logout revokes the refresh token.
- **AuthZ:** every project-scoped endpoint checks the caller is owner (or member) of the project via an authorization policy/handler; no endpoint trusts a client-supplied user id. This directly satisfies "users cannot access another user's projects."
- External/OAuth providers are deferred but the `User` model already tolerates an external identifier (§15-D2).

---

## 10. Configuration & environment strategy

- **Non-secret defaults** in `appsettings.json` / `appsettings.Development.json`.
- **Secrets** (DB password, JWT signing key, provider API keys) via **environment variables**; local dev uses a **gitignored `.env`** loaded by docker-compose, with a committed **`.env.example`** documenting every key.
- **Options pattern** with validated strongly-typed settings (`DatabaseOptions`, `JwtOptions`, `LlmOptions`, `EmbeddingOptions`, `StorageOptions`, `GitHubOptions`) validated at startup — fail fast on missing/invalid config.
- Frontend: `NEXT_PUBLIC_*` for the API base URL only; no secrets ever shipped to the browser.
- **Never commit secrets**; `.gitignore` covers `.env`, `.env.*`, uploaded files, build output.

---

## 11. Testing strategy

- **Unit (Domain/Application):** chunking, file filtering (allow/deny incl. secret files), validation, use-case logic, context assembly, citation extraction. Fast, no I/O.
- **Integration (Infrastructure/Api):** real Postgres+pgvector via **Testcontainers**; `WebApplicationFactory` for endpoint tests covering CRUD, auth, and **cross-tenant access denial**; vector round-trip; migration apply.
- **AI evaluation (later phases):** a small curated fixture project with question→expected-source pairs measuring retrieval relevance (hit@k), answer groundedness (does it cite / refuse appropriately), and citation accuracy. Provider calls mocked in CI by default; a manual/opt-in eval run hits real models. This satisfies "AI functionality should not only be tested manually."
- **Frontend:** Vitest + Testing Library for components/hooks; a few Playwright e2e flows for auth + ask-a-question.

---

## 12. Local development setup

- `docker-compose up` starts **Postgres+pgvector** (image `pgvector/pgvector`), the **API**, and the **web** app.
- Backend: `dotnet restore/build/test`, EF migrations applied on startup (dev) or via a script; health check at `/health`.
- Frontend: `npm install && npm run dev`.
- `.env.example` documents all required variables; a short README "getting started" section will list the exact commands.

---

## 13. Deployment strategy

- **Containerised** API and web; Postgres+pgvector as managed service or container.
- **CI (GitHub Actions):** restore → build → unit tests → integration tests (Testcontainers) → frontend lint/test/build → docker build. Secrets via GitHub Actions secrets, never in the repo.
- **CD (deferred, Phase 9):** target a container host (e.g. a single VM with compose, or a container platform / Azure Container Apps / Fly.io) + managed Postgres with pgvector. Exact target chosen at Phase 9; the container images make the platform interchangeable.

---

## 14. Development phases, milestones & exact build order

Follows the spec's 9 phases; each phase ends in a **working, tested, mergeable** slice (Definition of Done from the spec applies to every feature).

1. **Phase 1 — Foundation.** Repo skeleton (backend 4 projects + 2 test projects, frontend app), solution wiring, dependency rule, docker-compose (Postgres+pgvector), config/options + validation, Serilog, `/health`, `.gitignore`/`.env.example`, Git init, CI skeleton, landing page stub. *Milestone: `docker-compose up` yields a healthy API + running web + DB.*
2. **Phase 2 — Projects & Auth core.** User + JWT auth (register/login/refresh/logout), Project CRUD, `ProjectMember` (owner), authorization policy, integration tests incl. cross-tenant denial. *Milestone: authenticated project CRUD, provably isolated per user.*
3. **Phase 3 — File ingestion.** Upload endpoint (size limits, extension allowlist, secret denylist, path-traversal safe), `Document`/`SourceFile` metadata, `IFileStorage` (local), processing-status field, `IBackgroundJobQueue` (in-process). *Milestone: upload → document rows + Pending status.*
4. **Phase 4 — Text processing.** File readers, normalisation, line-aware chunking with metadata, background job that moves Document Pending→Processing→Completed. *Milestone: uploaded files become chunks with correct line ranges.*
5. **Phase 5 — RAG.** `IEmbeddingService`, `IVectorStore` (pgvector), embedding job, semantic search, context assembly, `IChatCompletionService`, grounded `AskQuestion` with citations; upgrade background jobs to Hangfire. *Milestone: ask a question, get a grounded, cited answer.*
6. **Phase 6 — AI chat.** Conversations + messages persistence, chat API, conversation history, SSE streaming. *Milestone: multi-turn chat UI with sources.*
7. **Phase 7 — GitHub integration.** `IGitHubRepositoryClient`, URL validation, clone/fetch, `.gitignore` + filter respect, repo ingestion reusing the Phase 3–5 pipeline, status. *Milestone: connect a public repo and query it.*
8. **Phase 8 — Agents & tools.** Agent abstraction + orchestration loop, tool registry with schemas/validation/logging, the four agents, `ToolExecution` observability. *Milestone: Code Explorer / Doc Generator / Bug Analysis / Architecture Analyst usable end-to-end.*
9. **Phase 9 — Production quality.** Rate limiting, hardened error handling, richer structured logging, usage tracking, expanded unit/integration/AI-eval coverage, Dockerfiles finalised, CI/CD, security pass (secrets, uploads, authz), docs. *Milestone: production-ready, deployable.*

**Cross-cutting from Phase 1:** clean architecture, strong typing, tests for important logic, no committed secrets, no swallowed errors.

---

## 15. Decisions (resolved 2026-07-27)

- **D1 — AI providers — ✅ REVISED (2026-07-27): Gemini embeddings + Ollama chat (both free).** Embeddings use Google Gemini `gemini-embedding-001` at **768 dims** (`outputDimensionality: 768`; `chunk_embeddings.embedding` is `vector(768)`) — free tier, key from Google AI Studio in `Gemini:ApiKey`. **Chat uses local Ollama** (`OllamaChatCompletionService`, default model `llama3.2`) because the user's Gemini free tier returns `generateContent` quota `limit: 0` (embeddings are free, text generation is not). Chat provider is config-switchable via **`Ai:ChatProvider` = `Ollama` | `Gemini`**; both `IChatCompletionService` impls sit behind the port, so enabling Gemini billing (or swapping to Claude/OpenAI) is config-only. *(Note: initial code defaulted to `text-embedding-004`/`gemini-2.0-flash`; corrected to `gemini-embedding-001` + Ollama after live-testing the key.)*
- **D2 — Auth — ✅ DECIDED: local email+password (JWT).** JWT access + rotating refresh tokens (httpOnly cookie), BCrypt hashing, revocable refresh tokens. External/OAuth deferred behind the same abstraction; `User` already tolerates an external identifier.
- **D3 — First milestone — ✅ DECIDED: Phase 1 only.** Build the foundation (repo skeleton, 4 backend projects + 2 test projects, Next.js app, docker-compose with Postgres+pgvector, config/options + validation, Serilog, `/health`, Git init, CI skeleton, landing stub), then review before Phase 2.

Everything else in this document is decided-with-documented-rationale unless you object.

---

## 16. Risks & trade-offs

| Risk / trade-off | Mitigation |
|---|---|
| **pgvector recall/perf** vs a dedicated vector DB | Fine for portfolio scale; `IVectorStore` keeps Qdrant/Pinecone swappable if needed. |
| **Embedding provider lock-in / dimension coupling** | Dimension is config-driven; re-embed job can rebuild the index if the model changes. |
| **Cost of embeddings/LLM on large repos** | Batching, extension filtering, size caps, usage tracking; local-embeddings option (D1-c) removes embedding cost. |
| **Background job durability** early on | Start with a simple queue; upgrade to Hangfire behind the same port at Phase 5 (low churn). |
| **Line-number citations** after normalisation | Line-preserving normalisation + chunker carries original line ranges; tested explicitly. |
| **Prompt injection via ingested content** | Treat retrieved content as data, not instructions; agents constrained to registered tools; strict system prompts. |
| **Cross-tenant data leakage** | Project-scoped retrieval + API authorization policy + integration tests asserting denial. |
| **Secrets in uploads / repos** | Denylist for `.env`, keys, certs, secrets; never store them; validated at ingestion. |
| **Scope creep (members/roles, multiple providers)** | Model for the future, ship owner-only first; providers behind interfaces. |
| **MediatR/AutoMapper/FluentAssertions licensing churn** | Avoided all three; lightweight handlers + manual/Mapster mapping + Shouldly assertions. |

---

## Appendix A — "spec was silent, I decided" register

| Topic | Decision | Rationale |
|---|---|---|
| Vector store | pgvector behind `IVectorStore` | Postgres-native, easy local, swappable. |
| CQRS mediator | None (plain use-case handlers) | Avoids MediatR commercial license + complexity. |
| Object mapping | Manual / Mapster | Avoids AutoMapper license. |
| Auth | Local JWT + rotating refresh (httpOnly cookie) + BCrypt | Self-contained, secure, demonstrable. |
| Background jobs | `IBackgroundJobQueue`; Channel→Hangfire(Postgres) | Simple first, durable later, low churn. |
| Streaming | SSE, graceful non-stream fallback | Matches "if practical." |
| API style | Controllers + `Asp.Versioning` | Clear versioned REST. |
| Logging | Serilog structured | Spec requirement. |
| Validation | FluentValidation + Options validation | Fail fast, testable. |
| File storage | Local disk behind `IFileStorage` | Easy local; S3/blob swappable. |
| Frontend data | TanStack Query + minimal Context | Fits polling/mutations without over-engineering. |
| Members/roles | Modelled now, owner-only shipped first | Future-proof without early complexity. |
| Reranker | `IReranker` no-op default | Matches "optionally." |
