# Phase 9 — Production Quality — Design

> Date: 2026-07-28 · Status: **approved, pre-implementation** · **Final phase.**
> Spec source of truth: [`PROJECT_SPEC.md`](../../../PROJECT_SPEC.md) § "Phase 9".
> Living plan: [`IMPLEMENTATION_PLAN.md`](../../../IMPLEMENTATION_PLAN.md) §13.

## 1. Goal & scope

Round out the app for production. Most of the spec's Phase-9 checklist already exists
(auth/authz, Serilog logging, `GlobalExceptionHandler`+ProblemDetails, 167 tests, multi-stage
Dockerfiles, GitHub Actions CI). This phase adds the four genuinely-missing pieces:

1. **Rate limiting** — protect auth + AI endpoints.
2. **Usage tracking** — the `UsageRecord` entity from the data model + a summary endpoint/UI.
3. **Security & hardening pass** — security headers, log enrichment, lock in safe error behavior.
4. **README / docs** — a proper portfolio front door.

**Out of scope:** re-doing what exists; parsing exact provider token counts (estimates used);
usage for background embeddings; per-endpoint billing/quotas beyond simple rate limits.

## 2. Decisions

| Area | Decision |
|------|----------|
| Rate limiter | ASP.NET Core built-in (`Microsoft.AspNetCore.RateLimiting`, no package). Policies: `auth` 10/min, `ai` 20/min, global default 100/min — all config-driven, partitioned by user id else client IP. 429 → ProblemDetails. |
| Testing & limits | Shared test factory sets limits effectively-infinite (existing tests unaffected); one dedicated test uses `factory.WithWebHostBuilder` to override `auth` to 2/window and asserts the 3rd request → 429. |
| Usage | `UsageRecord`(userId, projectId?, kind, tokensIn, tokensOut, costEstimate?, createdAt) + `UsageKind{Ask,Chat,AgentRun}`. Recorded via best-effort `IUsageRecorder` (failures logged, never thrown). Tokens **estimated** `⌈len/4⌉`. |
| Usage surface | `GET /api/v1/projects/{projectId}/usage` (owner-scoped) → totals + by-kind; compact card in the project Overview. |
| Security | Error handler already hides 500 detail — lock in with a test. Add security-headers middleware + per-request log enrichment (user id + correlation id). |

## 3. Rate limiting

Configured in `Program.cs` via `AddRateLimiter`. `RateLimitOptions` (section `RateLimit`):
`AuthPermitPerWindow=10`, `AiPermitPerWindow=20`, `GlobalPermitPerWindow=100`, `WindowSeconds=60`.

- **Partition key:** authenticated user id (`ClaimTypes`/`sub`) when present, else `HttpContext.Connection.RemoteIpAddress`.
- **Policies** (fixed-window): `auth`, `ai`. A **global limiter** (`GlobalLimiter`) applies to everything as a lenient backstop.
- **Application:** `[EnableRateLimiting("auth")]` on `AuthController`; `[EnableRateLimiting("ai")]` on `RagController`, `ConversationsController`, `AgentsController`. `app.UseRateLimiter()` after auth so the user id is available for partitioning.
- **Rejection:** `OnRejected` writes a 429 ProblemDetails (`"Too many requests."`, `Retry-After` header) — consistent with the rest of the API.

## 4. Usage tracking

**Domain:** `UsageRecord : Entity` (Id `ValueGeneratedNever`): `UserId`, `ProjectId` (non-null for our cases), `Kind` (`UsageKind` enum), `TokensIn`, `TokensOut`, `CostEstimate?` (decimal, null for now), `CreatedAt`. `UsageKind { Ask, Chat, AgentRun }`.

**Port:** `IUsageRecorder.RecordAsync(Guid userId, Guid projectId, UsageKind kind, int tokensIn, int tokensOut, CancellationToken ct)` — implementation creates the record, adds it via `IUsageRecordRepository`, saves via `IUnitOfWork`, all wrapped so any failure is logged (via `ILogger`) and swallowed (usage tracking must never break a user's answer).

**Estimation:** `TokenEstimator.Estimate(string) => (text.Length + 3) / 4`.

**Integration** (each already has user + project context):
- `RagService.AskAsync` → record `Ask` (in = system+context+question estimate, out = answer estimate) when grounded.
- `ChatService.SendMessageAsync` and `StreamMessageAsync` → record `Chat`.
- `AgentService.RunAsync` → record `AgentRun` (in = input, out = output).

**Query:** `IUsageRecordRepository.SummarizeByProjectAsync(projectId)` → aggregates. `UsageController` `GET /api/v1/projects/{projectId}/usage` (owner-checked) → `UsageSummaryResponse(int TotalRequests, long TotalTokensIn, long TotalTokensOut, IReadOnlyList<UsageByKind> ByKind)`, `UsageByKind(string Kind, int Requests, long TokensIn, long TokensOut)`.

**Persistence:** `usage_records` table (index `UserId`, `ProjectId`; FK→projects cascade, FK→users NoAction). Migration `AddUsageRecords`.

**Frontend:** a compact "AI usage" card in the project Overview aside — total requests + total tokens + a per-kind line; polled with the page. Types in `lib/types.ts`.

## 5. Security & hardening

- **Error leakage:** `GlobalExceptionHandler` already sets `Detail=null` for ≥500 and logs the exception; `AddProblemDetails()` does not include exception dumps. **No code change** — lock it in with a **unit test** on `GlobalExceptionHandler.TryHandleAsync` (substitute `IProblemDetailsService`, capture the `ProblemDetailsContext`): a generic `Exception` → status 500, `Detail == null`, `traceId` present; a `NotFoundException` → status 404, `Detail == message`. No contrived 500 endpoint needed.
- **Security headers middleware** (`SecurityHeadersMiddleware`, mapped early): `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` (fine for a JSON API). Applied to all responses.
- **Log enrichment** (`RequestContextMiddleware`): push `RequestId` (`HttpContext.TraceIdentifier`) and, when authenticated, `UserId` into Serilog `LogContext` for the request scope, so every log line is attributable. `UseSerilogRequestLogging` already logs method/path/status/duration and does **not** log headers or bodies (no secret leakage) — confirmed, no change.
- **CORS / size limits:** already config-driven (`Cors:AllowedOrigins`) and upload-capped; no change.
- **Test:** assert security headers present on a normal response; assert the forced-500 body is generic.

## 6. README / docs

Rewrite `README.md`: one-line pitch; the four pillars (grounded RAG chat with citations; GitHub repo ingestion; ReAct agents + tools; multi-tenant security); a mermaid architecture diagram; the 9-phase build summary; tech stack + the key "spec was silent, decided X" calls (pgvector, no MediatR/AutoMapper, JWT+refresh, Gemini embeddings + Ollama chat, ReAct); getting-started for **Docker compose** and **local dev**; how to run tests; env-var reference pointing at `.env.example`. Documentation only — no tests.

## 7. Testing

- **Unit:** `TokenEstimator`; `UsageRecorder` best-effort (records on success; swallows + logs on repo failure); `GlobalExceptionHandler` (generic 500 → no `Detail`; mapped exception → status + message).
- **Integration (Testcontainers):** rate limit → 3rd `auth` request over a 2/window override → 429 (isolated `WithWebHostBuilder` client); AI op records usage → `GET …/usage` reflects it; cross-tenant usage read → 404; security headers present on a normal response.
- Shared factory sets high rate limits so the existing 167 tests are unaffected.

## 8. Rough change checklist

- Domain: `UsageRecord`, `UsageKind`.
- Application: `IUsageRecorder`+`UsageRecorder`, `TokenEstimator`, `IUsageRecordRepository`, usage DTOs; inject recorder into `RagService`/`ChatService`/`AgentService`; `RateLimitOptions`.
- Infrastructure: EF config + `AddUsageRecords` migration; `UsageRecordRepository`; DI.
- Api: `AddRateLimiter` wiring + `[EnableRateLimiting]` attributes; `SecurityHeadersMiddleware`; `RequestContextMiddleware`; `UsageController`.
- Frontend: usage card in Overview + types.
- Docs: `README.md` rewrite.
- Tests: unit + integration per §7.
