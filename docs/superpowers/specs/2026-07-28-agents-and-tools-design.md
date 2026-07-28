# Phase 8 — Agents & Tools — Design

> Date: 2026-07-28 · Status: **approved, pre-implementation**
> Spec source of truth: [`PROJECT_SPEC.md`](../../../PROJECT_SPEC.md) § "AI Features" + "Tool Calling" + "Phase 8".
> Living plan: [`IMPLEMENTATION_PLAN.md`](../../../IMPLEMENTATION_PLAN.md) §7.

## 1. Goal & scope

Give each project four built-in AI agents that answer questions by **calling tools** over the
project's indexed content, with every tool call **observable** (persisted and shown).

**Milestone (from spec):** the four agents usable end-to-end.

**Agents**
- **Code Explorer** — locate + read + explain code ("Where is user registration implemented?").
- **Documentation Generator** — produce Markdown documentation for code/modules/APIs.
- **Bug Analysis** — from an error + stack trace (+ optional description): find related code, analyse,
  explain causes, suggest debug steps; **clearly separate codebase evidence from AI hypotheses**.
- **Architecture Analyst** — summarize technologies, major modules, dependencies.

**In scope:** ReAct-style tool-calling loop; 3 core tools; per-run + per-tool-call persistence; a
synchronous run API returning the answer + full trace; Agents + Documentation frontend tabs.

**Out of scope (this phase):** user-defined/custom agents (agents are built-in types); native
provider function-calling; streaming of agent steps; multi-turn agent conversations; tools beyond
the 3 core ones (SearchFiles / FindReferences / GetDocument / GenerateDocumentation-as-tool).

## 2. Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| Tool calls | **ReAct prompt loop** — model emits JSON `{action}` / `{final_answer}` as text; we parse, run, feed the observation back | Provider-agnostic; works with the free/local models (Ollama llama3.2 + Gemini) with **no change to the chat port**. |
| Execution | **Synchronous**, bounded loop, returns answer + full tool trace | Simpler than streaming; local multi-step loops are slow enough that a spinner + final trace suffices. Fully observable. |
| Tools | **3 core**: SearchProject, ReadFile, GetProjectStructure | Cover all four agents; defer the lower-value four. |
| Persistence | **Persist `AgentRun` + `ToolExecution` rows** | Satisfies "tool calls must be observable"; runs are re-viewable. |
| Agent defs | **Built-in types in code** (no user CRUD) | YAGNI for MVP; the `agents` table from the data model is deferred. |
| Iteration cap | **8** (config `AgentOptions.MaxIterations`) | Bounds cost/latency; failure is clean. |
| Run input | **Uniform `{ input: string }`** | Bug Analysis composes error/stacktrace/description into `input` client-side; keeps the API uniform. |

## 3. Architecture overview

```
POST /projects/{id}/agents/{agentType}/run  {input}
        │
        ▼
AgentService.RunAsync(user, project, agentType, input)
  ├─ ensure project owned
  ├─ def = AgentCatalog.For(agentType)          // system prompt + allowed tool names
  ├─ run = AgentRun.Start(project, user, agentType, input)
  ├─ messages = [system(def.prompt + tool docs + ReAct format), user(input)]
  ├─ loop i in 1..MaxIterations:
  │    completion = IChatCompletionService.CompleteAsync(system, messages)
  │    step = ReActParser.Parse(completion.Text)
  │    ├─ final_answer → run.Complete(answer); break
  │    ├─ action{tool,args} →
  │    │     tool = registry.Resolve(name, def.allowedTools)
  │    │     (out, status, ms) = try tool.ExecuteAsync(projectId, args) catch → (err, Error)
  │    │     run.AddToolExecution(seq, name, argsJson, out, status, err, ms)
  │    │     messages += assistant(completion.Text) + user("Observation:\n"+out)
  │    └─ unparetable → messages += user("Invalid format. Reply with JSON …")   // no tool exec
  ├─ if no final answer after cap → run.Fail("stopped after N iterations")
  ├─ uow.Save(run)                               // run + all tool executions, once
  └─ return AgentRunResponse(status, output, iterations, trace[])
```

Everything is behind small ports: `IAgentTool` + `ToolRegistry` (Application), `AgentCatalog`
(built-in defs), `ReActParser`, `AgentService`, `IAgentRunRepository`.

## 4. ReAct protocol

System prompt appends (a) the persona/instructions for the agent type, (b) a rendered list of the
allowed tools with their argument schemas, and (c) the strict format contract:

> Respond with EXACTLY ONE JSON object and nothing else. To use a tool:
> `{"thought": "...", "action": {"tool": "<name>", "arguments": { ... }}}`
> When you have enough information, give the final answer (Markdown allowed inside the string):
> `{"thought": "...", "final_answer": "..."}`

`ReActParser.Parse(text) → AgentStep` where `AgentStep` is one of:
- `Final(string answer, string? thought)`
- `Action(string tool, JsonElement arguments, string? thought)`
- `Unparseable(string raw)`

Parsing: locate the first balanced `{ … }` (tolerate ``` fences and surrounding prose), `JsonDocument.Parse`;
if it has `final_answer` → Final; else if `action.tool` → Action; else Unparseable.

## 5. Tools

`public interface IAgentTool { string Name; string Description; Task<string> ExecuteAsync(Guid projectId, JsonElement arguments, CancellationToken ct); }`
`Description` includes the argument schema and is rendered into the system prompt. Execution returns
the observation text. Missing/invalid arguments throw `ToolArgumentException` (caught → recorded → fed back).

- **SearchProject** — `{ "query": string, "topK"?: int }`. Calls `IRetrievalService.RetrieveAsync`;
  formats each hit as `#. path:startLine-endLine\n<snippet>`. Empty → "No matching content found."
- **ReadFile** — `{ "path": string }`. `IDocumentRepository.GetByPathAsync(projectId, path)` → read
  bytes via `IFileStorage.OpenReadAsync(storageKey)`; return up to `AgentOptions.MaxFileChars`
  (default 8000) with 1-based line-number prefixes; note truncation. Not found → "File not found: {path}".
- **GetProjectStructure** — `{}`. `IDocumentRepository.ListByProjectAsync` → sorted list of
  `path (fileType, status)`; header with count. Empty → "The project has no indexed files yet."

`ToolRegistry` maps name → `IAgentTool`; `Resolve(name, allowed)` returns null (→ "Unknown tool")
if the name isn't in the agent's allow-list or the registry.

## 6. Agents (`AgentCatalog`, built-in)

`AgentDefinition(AgentType Type, string DisplayName, string Description, string SystemPrompt, string[] Tools)`.
- **CodeExplorer** — Tools: Search, ReadFile, Structure. "Find and explain code; cite file paths + lines."
- **DocumentationGenerator** — Tools: Search, ReadFile, Structure. "Produce Markdown docs strictly from source you read; do not invent APIs."
- **BugAnalysis** — Tools: Search, ReadFile. "Investigate the error. Structure the answer as **## Evidence from the codebase** (only facts found via tools, with file:line) then **## Hypotheses** (clearly labelled reasoning) then **## Suggested debugging steps**."
- **ArchitectureAnalyst** — Tools: Structure, Search, ReadFile. "Summarize technologies, major modules, and dependencies from the actual files."

`AgentCatalog.All` (for `GET /agents`) and `AgentCatalog.For(type)` (throws NotFound for unknown).

## 7. Domain & persistence

- `AgentType` enum { CodeExplorer, DocumentationGenerator, BugAnalysis, ArchitectureAnalyst }.
- `AgentRun : Entity` (aggregate root, Id `ValueGeneratedNever`): `ProjectId`, `UserId`, `AgentType`,
  `Input`, `Output?`, `Status` (reuse `ProcessingStatus`: Processing→Completed/Failed), `Error?`,
  `Iterations`, owns `_toolExecutions`. Methods: `Start`, `AddToolExecution(...)→ToolExecution`,
  `Complete(output, iterations)`, `Fail(error, iterations)`.
- `ToolExecution : Entity` (Id `ValueGeneratedNever`): `AgentRunId`, `Sequence`, `ToolName`,
  `InputJson`, `OutputJson`, `Status` (`ToolExecutionStatus` { Ok, Error }), `Error?`, `DurationMs`.
- EF: `agent_runs` (index ProjectId, ProjectId+UserId; FK→projects cascade; FK→users NoAction),
  `tool_executions` (FK→agent_runs cascade, field-mapped collection). Migration `AddAgentRuns`.
- `IAgentRunRepository`: `GetWithToolExecutionsAsync(id)`, `ListByProjectAsync(projectId, userId)`,
  `AddAsync`. Ordered by `CreatedAt`.

## 8. API (`AgentsController`, `[Authorize]`, `/api/v1/projects/{projectId}/agents`)

| Verb | Path | Body | Result |
|------|------|------|--------|
| GET | `/` | — | `AgentInfo[]` (type, displayName, description) from `AgentCatalog` |
| POST | `/{agentType}/run` | `{ input }` | `AgentRunResponse` (id, agentType, status, output, iterations, trace[], createdAt) |
| GET | `/runs` | — | `AgentRunSummary[]` (newest first) |
| GET | `/runs/{runId}` | — | `AgentRunResponse` with full trace |

`agentType` is the enum name (case-insensitive). Unknown type → 404. Empty input → 400. Ownership
enforced via the project-owner check; cross-tenant → 404. `TraceItem(sequence, toolName, input, output, status, error, durationMs)`.

## 9. Frontend

- **Agents tab** (`components/project-agents.tsx`): a segmented control for Code Explorer / Bug
  Analysis / Architecture Analyst; input area (Bug Analysis shows error + stack-trace + description
  fields composed into `input`); **Run** → Markdown answer (reuses `Markdown`) + an expandable
  **tool trace** (`components/agent-trace.tsx`: each step's tool, args, output, duration, status);
  a collapsible past-runs list (`GET /runs` filtered by the selected type).
- **Documentation tab** (`components/project-documentation.tsx`): the Documentation Generator —
  input "what to document" → rendered Markdown doc + trace.
- Types added to `lib/types.ts`; both call the run endpoint via `authFetch`. Runs can be slow
  (local model) → a clear pending state; no polling (synchronous response).

## 10. Configuration & security

- `AgentOptions` (section `Agent`): `MaxIterations=8`, `MaxFileChars=8000`, `SearchTopK=6`.
- Ownership on every endpoint; runs + tools scoped to the project. Tool outputs are treated as data.
- Retrieved/file content is never executed; the agent can only call the registered tools with
  validated arguments (no arbitrary file-system access — ReadFile resolves through the document
  table + storage key, never a raw path).
- Iteration + file-size caps bound cost and prevent runaway loops.

## 11. Testing

- **Unit:** `ReActParser` (action / final / fenced / prose-wrapped / malformed); each tool (fakes for
  retrieval/repo/storage; not-found + truncation + empty); `AgentService` loop with a scripted fake
  `IChatCompletionService` — action→observation→final_answer (asserts tool ran, trace recorded,
  answer returned), plus max-iterations→Fail, unknown-tool observation, tool-throws→recorded-Error;
  ownership → NotFound; empty input → Validation.
- **Integration (Testcontainers):** the shared `FakeChatCompletionService` gains **agent awareness** —
  it detects an agent request by the ReAct contract in the system prompt and returns a SearchProject
  action on the first call, then a `final_answer` once an `Observation:` is present; for non-agent
  (RAG/chat) requests it returns the existing canned text unchanged (so prior tests keep passing).
  Then → connect an indexed
  project, `POST /agents/CodeExplorer/run`, assert Completed + non-empty output + one persisted
  SearchProject `ToolExecution`; `GET /runs/{id}` returns the trace; cross-tenant `run`/`get` → 404;
  unknown agent type → 404.

## 12. Rough change checklist

- Domain: `AgentType`, `ToolExecutionStatus` enums; `AgentRun`, `ToolExecution` entities.
- Application: `IAgentTool` + `ToolRegistry`; `SearchProjectTool` / `ReadFileTool` /
  `GetProjectStructureTool`; `AgentCatalog` + `AgentDefinition`; `ReActParser` + `AgentStep`;
  `AgentService`; `AgentOptions`; DTOs; `IAgentRunRepository`; `IDocumentRepository.GetByPathAsync`.
- Infrastructure: EF configs + `AddAgentRuns` migration; `AgentRunRepository`; `GetByPathAsync` impl; DI.
- Api: `AgentsController`.
- Frontend: `project-agents.tsx`, `project-documentation.tsx`, `agent-trace.tsx`, types, tab wiring.
- Tests: agent-aware fake chat provider; unit + integration per §11.
