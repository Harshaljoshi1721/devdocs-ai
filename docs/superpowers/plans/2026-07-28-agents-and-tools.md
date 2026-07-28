# Agents & Tools (Phase 8) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Four built-in AI agents (Code Explorer, Documentation Generator, Bug Analysis, Architecture Analyst) that answer questions by calling tools over a project's indexed content via a ReAct loop, with every tool call persisted and observable.

**Architecture:** An `AgentService` runs a bounded ReAct loop: the chat model emits JSON (`{action}` or `{final_answer}`) as text; `ReActParser` parses it; on an action we run an `IAgentTool` from the `ToolRegistry`, record a `ToolExecution` on the `AgentRun` aggregate, and feed the observation back. Runs are synchronous and return the answer plus the full trace. No change to the chat port (tools ride on plain text completion).

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, `System.Text.Json`, xUnit + Shouldly + NSubstitute + Testcontainers; Next.js 16 / React 19 / TanStack Query.

**Design source:** [`docs/superpowers/specs/2026-07-28-agents-and-tools-design.md`](../specs/2026-07-28-agents-and-tools-design.md).

> **Git:** work on branch `phase-8-agents` (branch from `main`). Per-task commits; the user gates any push (none expected). Run backend commands from `backend/`, frontend from `frontend/`. Docker required for integration tests.

> **Setup (do once before Task 1):**
> ```bash
> cd /Users/harshaljoshi/Projects/DevDocs-AI && git switch -c phase-8-agents
> ```

---

## File structure

**Domain** — `Enums/AgentType.cs`, `Enums/ToolExecutionStatus.cs`, `Entities/ToolExecution.cs`, `Entities/AgentRun.cs`
**Application** (`Features/Agents/`) — `AgentOptions.cs`, `ReActParser.cs`, `IAgentTool.cs` (+ `ToolNames`, `ToolArgs`, `ToolRegistry`), `Tools/SearchProjectTool.cs`, `Tools/ReadFileTool.cs`, `Tools/GetProjectStructureTool.cs`, `AgentCatalog.cs`, `AgentDtos.cs`, `AgentService.cs`; modify `Abstractions/Persistence/IDocumentRepository.cs` (+`GetByPathAsync`), `Abstractions/Persistence/Repositories.cs` (+`IAgentRunRepository`), `DependencyInjection.cs`
**Infrastructure** — `Persistence/Configurations/AgentRunConfiguration.cs`, `.../ToolExecutionConfiguration.cs`, `Persistence/Repositories/AgentRunRepository.cs`; modify `AppDbContext.cs`, `Repositories/DocumentRepository.cs`, `DependencyInjection.cs`; migration `AddAgentRuns`; `Api/appsettings.json`
**Api** — `Controllers/AgentsController.cs`
**Frontend** — `lib/types.ts`, `components/agent-trace.tsx`, `components/project-agents.tsx`, `components/project-documentation.tsx`, `app/projects/[id]/page.tsx`
**Tests** — `UnitTests/Features/ReActParserTests.cs`, `AgentToolsTests.cs`, `AgentServiceTests.cs`; `IntegrationTests/Infrastructure/FakeAiServices.cs` (modify), `IntegrationTests/AgentEndpointsTests.cs`

---

## Task 1: Domain — `AgentRun` + `ToolExecution`

**Files:** Create `src/DevDocsAI.Domain/Enums/AgentType.cs`, `src/DevDocsAI.Domain/Enums/ToolExecutionStatus.cs`, `src/DevDocsAI.Domain/Entities/ToolExecution.cs`, `src/DevDocsAI.Domain/Entities/AgentRun.cs`; Test `tests/DevDocsAI.UnitTests/Features/AgentRunTests.cs`

- [ ] **Step 1: Write the failing test** — `tests/DevDocsAI.UnitTests/Features/AgentRunTests.cs`:
```csharp
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class AgentRunTests
{
    private readonly Guid _projectId = Guid.CreateVersion7();
    private readonly Guid _userId = Guid.CreateVersion7();

    [Fact]
    public void Start_creates_a_processing_run()
    {
        var run = AgentRun.Start(_projectId, _userId, AgentType.CodeExplorer, "where is auth?");

        run.ProjectId.ShouldBe(_projectId);
        run.UserId.ShouldBe(_userId);
        run.AgentType.ShouldBe(AgentType.CodeExplorer);
        run.Input.ShouldBe("where is auth?");
        run.Status.ShouldBe(ProcessingStatus.Processing);
        run.ToolExecutions.ShouldBeEmpty();
    }

    [Fact]
    public void AddToolExecution_appends_in_order_bound_to_the_run()
    {
        var run = AgentRun.Start(_projectId, _userId, AgentType.BugAnalysis, "err");

        var te = run.AddToolExecution(1, "SearchProject", "{\"query\":\"x\"}", "hit", ToolExecutionStatus.Ok, null, 12);

        te.AgentRunId.ShouldBe(run.Id);
        te.Sequence.ShouldBe(1);
        te.ToolName.ShouldBe("SearchProject");
        te.Status.ShouldBe(ToolExecutionStatus.Ok);
        run.ToolExecutions.ShouldHaveSingleItem().ShouldBe(te);
    }

    [Fact]
    public void Complete_sets_output_and_completed_status()
    {
        var run = AgentRun.Start(_projectId, _userId, AgentType.CodeExplorer, "q");
        run.Complete("the answer", 3);
        run.Status.ShouldBe(ProcessingStatus.Completed);
        run.Output.ShouldBe("the answer");
        run.Iterations.ShouldBe(3);
        run.Error.ShouldBeNull();
    }

    [Fact]
    public void Fail_records_error_and_failed_status()
    {
        var run = AgentRun.Start(_projectId, _userId, AgentType.CodeExplorer, "q");
        run.Fail("stopped after 8 iterations", 8);
        run.Status.ShouldBe(ProcessingStatus.Failed);
        run.Error.ShouldBe("stopped after 8 iterations");
        run.Iterations.ShouldBe(8);
    }
}
```

- [ ] **Step 2: Run — expect FAIL** (compile): `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~AgentRunTests"`

- [ ] **Step 3: Create the enums**

`src/DevDocsAI.Domain/Enums/AgentType.cs`:
```csharp
namespace DevDocsAI.Domain.Enums;

/// <summary>The built-in agent personas available to every project.</summary>
public enum AgentType
{
    CodeExplorer = 0,
    DocumentationGenerator = 1,
    BugAnalysis = 2,
    ArchitectureAnalyst = 3,
}
```
`src/DevDocsAI.Domain/Enums/ToolExecutionStatus.cs`:
```csharp
namespace DevDocsAI.Domain.Enums;

/// <summary>Outcome of a single tool invocation within an agent run.</summary>
public enum ToolExecutionStatus
{
    Ok = 0,
    Error = 1,
}
```

- [ ] **Step 4: Create `ToolExecution`** — `src/DevDocsAI.Domain/Entities/ToolExecution.cs`:
```csharp
using DevDocsAI.Domain.Common;
using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// One recorded tool invocation inside an <see cref="AgentRun"/>: what was called,
/// the JSON input, the observation returned, and how it went. The trace is the
/// project's audit of how an answer was produced.
/// </summary>
public sealed class ToolExecution : Entity
{
    private ToolExecution() { } // EF

    internal ToolExecution(
        Guid agentRunId, int sequence, string toolName, string inputJson,
        string outputJson, ToolExecutionStatus status, string? error, long durationMs)
    {
        AgentRunId = agentRunId;
        Sequence = sequence;
        ToolName = toolName;
        InputJson = inputJson;
        OutputJson = outputJson;
        Status = status;
        Error = error;
        DurationMs = durationMs;
    }

    public Guid AgentRunId { get; private set; }
    public int Sequence { get; private set; }
    public string ToolName { get; private set; } = null!;
    public string InputJson { get; private set; } = null!;
    public string OutputJson { get; private set; } = null!;
    public ToolExecutionStatus Status { get; private set; }
    public string? Error { get; private set; }
    public long DurationMs { get; private set; }
}
```

- [ ] **Step 5: Create `AgentRun`** — `src/DevDocsAI.Domain/Entities/AgentRun.cs`:
```csharp
using DevDocsAI.Domain.Common;
using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Domain.Entities;

/// <summary>
/// A single execution of an agent against a project. Aggregate root for its
/// <see cref="ToolExecution"/> trace; tool executions are only created through
/// <see cref="AddToolExecution"/> so they stay consistent.
/// </summary>
public sealed class AgentRun : Entity
{
    private readonly List<ToolExecution> _toolExecutions = [];

    private AgentRun() { } // EF

    private AgentRun(Guid projectId, Guid userId, AgentType agentType, string input)
    {
        ProjectId = projectId;
        UserId = userId;
        AgentType = agentType;
        Input = input;
        Status = ProcessingStatus.Processing;
    }

    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public AgentType AgentType { get; private set; }
    public string Input { get; private set; } = null!;
    public string? Output { get; private set; }
    public ProcessingStatus Status { get; private set; }
    public string? Error { get; private set; }
    public int Iterations { get; private set; }

    public IReadOnlyList<ToolExecution> ToolExecutions => _toolExecutions.AsReadOnly();

    public static AgentRun Start(Guid projectId, Guid userId, AgentType agentType, string input) =>
        new(projectId, userId, agentType, input);

    public ToolExecution AddToolExecution(
        int sequence, string toolName, string inputJson, string outputJson,
        ToolExecutionStatus status, string? error, long durationMs)
    {
        var te = new ToolExecution(Id, sequence, toolName, inputJson, outputJson, status, error, durationMs);
        _toolExecutions.Add(te);
        return te;
    }

    public void Complete(string output, int iterations)
    {
        Output = output;
        Iterations = iterations;
        Status = ProcessingStatus.Completed;
        Error = null;
    }

    public void Fail(string error, int iterations)
    {
        Error = error;
        Iterations = iterations;
        Status = ProcessingStatus.Failed;
    }
}
```

- [ ] **Step 6: Run — expect PASS** (4 tests): `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~AgentRunTests"`

- [ ] **Step 7: Commit**
```bash
git add src/DevDocsAI.Domain tests/DevDocsAI.UnitTests/Features/AgentRunTests.cs
git commit -m "feat(domain): AgentRun + ToolExecution entities"
```

---

## Task 2: `ReActParser`

**Files:** Create `src/DevDocsAI.Application/Features/Agents/ReActParser.cs`; Test `tests/DevDocsAI.UnitTests/Features/ReActParserTests.cs`

- [ ] **Step 1: Write the failing test** — `tests/DevDocsAI.UnitTests/Features/ReActParserTests.cs`:
```csharp
using DevDocsAI.Application.Features.Agents;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class ReActParserTests
{
    [Fact]
    public void Parses_a_final_answer()
    {
        var step = ReActParser.Parse("""{"thought":"done","final_answer":"It uses JWT."}""");
        var final = step.ShouldBeOfType<FinalStep>();
        final.Answer.ShouldBe("It uses JWT.");
    }

    [Fact]
    public void Parses_an_action_with_arguments()
    {
        var step = ReActParser.Parse("""{"action":{"tool":"SearchProject","arguments":{"query":"auth"}}}""");
        var action = step.ShouldBeOfType<ActionStep>();
        action.Tool.ShouldBe("SearchProject");
        action.Arguments.GetProperty("query").GetString().ShouldBe("auth");
    }

    [Fact]
    public void Parses_action_when_arguments_are_omitted()
    {
        var step = ReActParser.Parse("""{"action":{"tool":"GetProjectStructure"}}""");
        var action = step.ShouldBeOfType<ActionStep>();
        action.Tool.ShouldBe("GetProjectStructure");
        action.Arguments.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public void Extracts_json_from_code_fences_and_surrounding_prose()
    {
        var raw = "Sure!\n```json\n{\"final_answer\":\"hi\"}\n```\nHope that helps.";
        ReActParser.Parse(raw).ShouldBeOfType<FinalStep>().Answer.ShouldBe("hi");
    }

    [Fact]
    public void Returns_unparseable_for_non_json_or_wrong_shape()
    {
        ReActParser.Parse("I don't know how to respond").ShouldBeOfType<UnparseableStep>();
        ReActParser.Parse("""{"something":"else"}""").ShouldBeOfType<UnparseableStep>();
    }
}
```

- [ ] **Step 2: Run — expect FAIL** (compile): `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~ReActParserTests"`

- [ ] **Step 3: Implement** — `src/DevDocsAI.Application/Features/Agents/ReActParser.cs`:
```csharp
using System.Text.Json;

namespace DevDocsAI.Application.Features.Agents;

/// <summary>One step the model produced in the ReAct loop.</summary>
public abstract record AgentStep;
public sealed record FinalStep(string Answer, string? Thought) : AgentStep;
public sealed record ActionStep(string Tool, JsonElement Arguments, string? Thought) : AgentStep;
public sealed record UnparseableStep(string Raw) : AgentStep;

/// <summary>
/// Parses a model turn into a ReAct step. Tolerates code fences and surrounding
/// prose by extracting the first balanced JSON object, then reading either a
/// <c>final_answer</c> or an <c>action</c> with a tool name.
/// </summary>
public static class ReActParser
{
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    public static AgentStep Parse(string text)
    {
        if (!TryExtractJson(text, out var json))
            return new UnparseableStep(text);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return new UnparseableStep(text); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new UnparseableStep(text);

            var thought = root.TryGetProperty("thought", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;

            if (root.TryGetProperty("final_answer", out var fa) && fa.ValueKind == JsonValueKind.String)
                return new FinalStep(fa.GetString() ?? string.Empty, thought);

            if (root.TryGetProperty("action", out var action) && action.ValueKind == JsonValueKind.Object &&
                action.TryGetProperty("tool", out var tool) && tool.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(tool.GetString()))
            {
                var arguments = action.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.Object
                    ? args.Clone()
                    : EmptyObject;
                return new ActionStep(tool.GetString()!, arguments, thought);
            }

            return new UnparseableStep(text);
        }
    }

    /// <summary>Extracts the substring from the first '{' to its matching '}'.</summary>
    private static bool TryExtractJson(string text, out string json)
    {
        json = string.Empty;
        var start = text.IndexOf('{');
        if (start < 0) return false;

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0) { json = text[start..(i + 1)]; return true; }
            }
        }

        return false;
    }
}
```

- [ ] **Step 4: Run — expect PASS** (5 tests): `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~ReActParserTests"`

- [ ] **Step 5: Commit**
```bash
git add src/DevDocsAI.Application/Features/Agents/ReActParser.cs tests/DevDocsAI.UnitTests/Features/ReActParserTests.cs
git commit -m "feat(app): ReAct parser (action / final_answer / tolerant JSON)"
```

---

## Task 3: Tool contracts + options

**Files:** Create `src/DevDocsAI.Application/Features/Agents/AgentOptions.cs`, `src/DevDocsAI.Application/Features/Agents/IAgentTool.cs`. No test (used by Task 4).

- [ ] **Step 1: Options** — `src/DevDocsAI.Application/Features/Agents/AgentOptions.cs`:
```csharp
namespace DevDocsAI.Application.Features.Agents;

/// <summary>Bounds for agent runs, bound from the "Agent" config section.</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Maximum ReAct iterations before the run is stopped and marked failed.</summary>
    public int MaxIterations { get; init; } = 8;

    /// <summary>Maximum characters returned by ReadFile before truncation.</summary>
    public int MaxFileChars { get; init; } = 8000;

    /// <summary>Default number of results for SearchProject.</summary>
    public int SearchTopK { get; init; } = 6;
}
```

- [ ] **Step 2: Tool contract + registry** — `src/DevDocsAI.Application/Features/Agents/IAgentTool.cs`:
```csharp
using System.Text;
using System.Text.Json;

namespace DevDocsAI.Application.Features.Agents;

/// <summary>Canonical tool names, shared by the tools and the agent catalog.</summary>
public static class ToolNames
{
    public const string SearchProject = "SearchProject";
    public const string ReadFile = "ReadFile";
    public const string GetProjectStructure = "GetProjectStructure";
}

/// <summary>
/// A capability an agent can invoke. <see cref="Description"/> documents the
/// arguments and is rendered into the system prompt. Execution returns the
/// observation text fed back to the model. Invalid arguments throw; the loop
/// records the failure and feeds the message back.
/// </summary>
public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    Task<string> ExecuteAsync(Guid projectId, JsonElement arguments, CancellationToken ct);
}

/// <summary>Helpers for reading tool arguments with clear failures.</summary>
public static class ToolArgs
{
    public static string RequireString(JsonElement args, string name)
    {
        if (args.ValueKind == JsonValueKind.Object &&
            args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
        {
            var s = v.GetString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        throw new InvalidOperationException($"Missing required string argument '{name}'.");
    }

    public static int? OptionalInt(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object &&
        args.TryGetProperty(name, out var v) && v.TryGetInt32(out var i)
            ? i
            : null;
}

/// <summary>Resolves tools by name (honouring an agent's allow-list) and describes them for prompts.</summary>
public sealed class ToolRegistry(IEnumerable<IAgentTool> tools)
{
    private readonly Dictionary<string, IAgentTool> _byName =
        tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    public IAgentTool? Resolve(string name, IReadOnlyCollection<string> allowed) =>
        allowed.Contains(name, StringComparer.OrdinalIgnoreCase) && _byName.TryGetValue(name, out var t)
            ? t
            : null;

    public string Describe(IReadOnlyCollection<string> allowed)
    {
        var sb = new StringBuilder();
        foreach (var name in allowed)
        {
            if (_byName.TryGetValue(name, out var t))
                sb.AppendLine($"- {t.Name}: {t.Description}");
        }

        return sb.ToString().TrimEnd();
    }
}
```

- [ ] **Step 3: Build** — `dotnet build src/DevDocsAI.Application/DevDocsAI.Application.csproj` — Expected: succeeded, 0 warnings.

- [ ] **Step 4: Commit**
```bash
git add src/DevDocsAI.Application/Features/Agents/AgentOptions.cs src/DevDocsAI.Application/Features/Agents/IAgentTool.cs
git commit -m "feat(app): agent tool contract, registry, options"
```

---

## Task 4: The three tools + `GetByPathAsync`

**Files:** Modify `src/DevDocsAI.Application/Abstractions/Persistence/IDocumentRepository.cs`; Create `src/DevDocsAI.Application/Features/Agents/Tools/SearchProjectTool.cs`, `.../ReadFileTool.cs`, `.../GetProjectStructureTool.cs`; Modify `src/DevDocsAI.Infrastructure/Persistence/Repositories/DocumentRepository.cs`; Test `tests/DevDocsAI.UnitTests/Features/AgentToolsTests.cs`

- [ ] **Step 1: Add repo port method** — in `src/DevDocsAI.Application/Abstractions/Persistence/IDocumentRepository.cs`, add inside the interface:
```csharp
    Task<Document?> GetByPathAsync(Guid projectId, string path, CancellationToken ct);
```

- [ ] **Step 2: Write the failing test** — `tests/DevDocsAI.UnitTests/Features/AgentToolsTests.cs`:
```csharp
using System.Text;
using System.Text.Json;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Abstractions.Storage;
using DevDocsAI.Application.Features.Agents;
using DevDocsAI.Application.Features.Agents.Tools;
using DevDocsAI.Application.Features.Rag;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class AgentToolsTests
{
    private readonly Guid _projectId = Guid.CreateVersion7();
    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();
    private static readonly IOptions<AgentOptions> Options = Microsoft.Extensions.Options.Options.Create(new AgentOptions());

    [Fact]
    public async Task SearchProject_formats_hits_with_locations()
    {
        var retrieval = Substitute.For<IRetrievalService>();
        retrieval.RetrieveAsync(_projectId, "auth", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SearchHit>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "auth.cs", "src/auth.cs", 10, 25, 0.9, "class Auth {}"),
            });
        var tool = new SearchProjectTool(retrieval, Options);

        var result = await tool.ExecuteAsync(_projectId, Args("""{"query":"auth"}"""), default);

        result.ShouldContain("src/auth.cs:10-25");
        result.ShouldContain("class Auth {}");
    }

    [Fact]
    public async Task SearchProject_reports_when_empty()
    {
        var retrieval = Substitute.For<IRetrievalService>();
        retrieval.RetrieveAsync(_projectId, Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SearchHit>());
        var tool = new SearchProjectTool(retrieval, Options);

        (await tool.ExecuteAsync(_projectId, Args("""{"query":"x"}"""), default))
            .ShouldContain("No matching");
    }

    [Fact]
    public async Task SearchProject_missing_query_throws()
    {
        var tool = new SearchProjectTool(Substitute.For<IRetrievalService>(), Options);
        await Should.ThrowAsync<InvalidOperationException>(
            () => tool.ExecuteAsync(_projectId, Args("{}"), default));
    }

    [Fact]
    public async Task ReadFile_returns_numbered_content()
    {
        var documents = Substitute.For<IDocumentRepository>();
        var storage = Substitute.For<IFileStorage>();
        var doc = new Document(_projectId, "auth.cs", "src/auth.cs", FileType.Code, "h", 10, "key-1");
        documents.GetByPathAsync(_projectId, "src/auth.cs", Arg.Any<CancellationToken>()).Returns(doc);
        storage.OpenReadAsync("key-1", Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes("line one\nline two")));
        var tool = new ReadFileTool(documents, storage, Options);

        var result = await tool.ExecuteAsync(_projectId, Args("""{"path":"src/auth.cs"}"""), default);

        result.ShouldContain("1\tline one");
        result.ShouldContain("2\tline two");
    }

    [Fact]
    public async Task ReadFile_reports_not_found()
    {
        var documents = Substitute.For<IDocumentRepository>();
        documents.GetByPathAsync(_projectId, "missing", Arg.Any<CancellationToken>()).Returns((Document?)null);
        var tool = new ReadFileTool(documents, Substitute.For<IFileStorage>(), Options);

        (await tool.ExecuteAsync(_projectId, Args("""{"path":"missing"}"""), default))
            .ShouldContain("not found");
    }

    [Fact]
    public async Task GetProjectStructure_lists_paths()
    {
        var documents = Substitute.For<IDocumentRepository>();
        documents.ListByProjectAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(new List<Document> { new(_projectId, "a.cs", "src/a.cs", FileType.Code, "h", 1, "k") });
        var tool = new GetProjectStructureTool(documents);

        (await tool.ExecuteAsync(_projectId, Args("{}"), default)).ShouldContain("src/a.cs");
    }
}
```

- [ ] **Step 3: Run — expect FAIL** (compile): `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~AgentToolsTests"`

- [ ] **Step 4: Implement `SearchProjectTool`** — `src/DevDocsAI.Application/Features/Agents/Tools/SearchProjectTool.cs`:
```csharp
using System.Text;
using System.Text.Json;
using DevDocsAI.Application.Features.Rag;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Application.Features.Agents.Tools;

/// <summary>Semantic search over the project's indexed chunks (reuses RAG retrieval).</summary>
public sealed class SearchProjectTool(IRetrievalService retrieval, IOptions<AgentOptions> options) : IAgentTool
{
    private readonly AgentOptions _options = options.Value;

    public string Name => ToolNames.SearchProject;
    public string Description =>
        "Semantic code/doc search. Arguments: { \"query\": string, \"topK\"?: number }. " +
        "Returns ranked snippets with file paths and line ranges.";

    public async Task<string> ExecuteAsync(Guid projectId, JsonElement arguments, CancellationToken ct)
    {
        var query = ToolArgs.RequireString(arguments, "query");
        var topK = ToolArgs.OptionalInt(arguments, "topK") ?? _options.SearchTopK;

        var hits = await retrieval.RetrieveAsync(projectId, query, topK, ct);
        if (hits.Count == 0) return "No matching content found.";

        var sb = new StringBuilder();
        var i = 1;
        foreach (var h in hits)
        {
            sb.AppendLine($"{i}. {h.Path}:{h.StartLine}-{h.EndLine}");
            sb.AppendLine(h.Snippet);
            sb.AppendLine();
            i++;
        }

        return sb.ToString().TrimEnd();
    }
}
```

- [ ] **Step 5: Implement `ReadFileTool`** — `src/DevDocsAI.Application/Features/Agents/Tools/ReadFileTool.cs`:
```csharp
using System.Text;
using System.Text.Json;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Application.Features.Agents.Tools;

/// <summary>Reads an indexed file's text by its project-relative path, with line numbers.</summary>
public sealed class ReadFileTool(
    IDocumentRepository documents, IFileStorage fileStorage, IOptions<AgentOptions> options) : IAgentTool
{
    private readonly AgentOptions _options = options.Value;

    public string Name => ToolNames.ReadFile;
    public string Description =>
        "Read one indexed file. Arguments: { \"path\": string } (use a path from SearchProject or " +
        "GetProjectStructure). Returns line-numbered content.";

    public async Task<string> ExecuteAsync(Guid projectId, JsonElement arguments, CancellationToken ct)
    {
        var path = ToolArgs.RequireString(arguments, "path");
        var doc = await documents.GetByPathAsync(projectId, path, ct);
        if (doc is null) return $"File not found: {path}";

        await using var stream = await fileStorage.OpenReadAsync(doc.StorageKey, ct);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(ct);

        var truncated = content.Length > _options.MaxFileChars;
        if (truncated) content = content[.._options.MaxFileChars];

        var lines = content.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        sb.AppendLine($"File: {path}");
        for (var i = 0; i < lines.Length; i++)
            sb.AppendLine($"{i + 1}\t{lines[i]}");
        if (truncated) sb.AppendLine($"… (truncated at {_options.MaxFileChars} characters)");

        return sb.ToString().TrimEnd();
    }
}
```

- [ ] **Step 6: Implement `GetProjectStructureTool`** — `src/DevDocsAI.Application/Features/Agents/Tools/GetProjectStructureTool.cs`:
```csharp
using System.Text;
using System.Text.Json;
using DevDocsAI.Application.Abstractions.Persistence;

namespace DevDocsAI.Application.Features.Agents.Tools;

/// <summary>Lists the project's indexed files (path, type, status).</summary>
public sealed class GetProjectStructureTool(IDocumentRepository documents) : IAgentTool
{
    public string Name => ToolNames.GetProjectStructure;
    public string Description => "List all indexed files in the project. Arguments: {} (none).";

    public async Task<string> ExecuteAsync(Guid projectId, JsonElement arguments, CancellationToken ct)
    {
        var docs = await documents.ListByProjectAsync(projectId, ct);
        if (docs.Count == 0) return "The project has no indexed files yet.";

        var sb = new StringBuilder();
        sb.AppendLine($"{docs.Count} file(s):");
        foreach (var d in docs.OrderBy(d => d.Path, StringComparer.Ordinal))
            sb.AppendLine($"- {d.Path} ({d.FileType}, {d.ProcessingStatus})");

        return sb.ToString().TrimEnd();
    }
}
```

- [ ] **Step 7: Implement `GetByPathAsync`** — in `src/DevDocsAI.Infrastructure/Persistence/Repositories/DocumentRepository.cs`, add:
```csharp
    public Task<Document?> GetByPathAsync(Guid projectId, string path, CancellationToken ct) =>
        db.Documents
            .Where(d => d.ProjectId == projectId && d.Path == path)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);
```

- [ ] **Step 8: Run — expect PASS** (6 tests): `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~AgentToolsTests"`

- [ ] **Step 9: Commit**
```bash
git add src/DevDocsAI.Application src/DevDocsAI.Infrastructure/Persistence/Repositories/DocumentRepository.cs tests/DevDocsAI.UnitTests/Features/AgentToolsTests.cs
git commit -m "feat(app): SearchProject/ReadFile/GetProjectStructure tools + GetByPathAsync"
```

---

## Task 5: `AgentCatalog`

**Files:** Create `src/DevDocsAI.Application/Features/Agents/AgentCatalog.cs`. No dedicated test (exercised by AgentService tests).

- [ ] **Step 1: Implement** — `src/DevDocsAI.Application/Features/Agents/AgentCatalog.cs`:
```csharp
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Application.Features.Agents;

/// <summary>A built-in agent: its persona system prompt and the tools it may use.</summary>
public sealed record AgentDefinition(
    AgentType Type, string DisplayName, string Description, string SystemPrompt, string[] Tools);

/// <summary>The four built-in agents. Definitions live in code (no user-defined agents in the MVP).</summary>
public static class AgentCatalog
{
    private static readonly string[] AllTools =
        [ToolNames.SearchProject, ToolNames.ReadFile, ToolNames.GetProjectStructure];

    public static readonly IReadOnlyList<AgentDefinition> All =
    [
        new(AgentType.CodeExplorer, "Code Explorer",
            "Find and explain where things are implemented.",
            "You are Code Explorer, a precise codebase navigator. Use the tools to locate and read " +
            "the relevant files, then explain clearly. Always cite file paths and line ranges. Base " +
            "every claim on file content you actually read — never guess.",
            AllTools),

        new(AgentType.DocumentationGenerator, "Documentation Generator",
            "Generate Markdown documentation from the source.",
            "You are Documentation Generator. Produce clear, well-structured Markdown documentation for " +
            "what the user asks about, based ONLY on source you read via the tools. Do not invent APIs, " +
            "parameters, or behavior. Include a short overview then details, and reference file paths.",
            AllTools),

        new(AgentType.BugAnalysis, "Bug Analysis",
            "Analyse an error and suggest debugging steps.",
            "You are Bug Analysis. Investigate the reported error using the tools. Structure your final " +
            "answer with these Markdown sections, in order: '## Evidence from the codebase' (ONLY facts " +
            "found via tools, each with file:line), '## Hypotheses' (clearly-labelled reasoning that goes " +
            "beyond the evidence), and '## Suggested debugging steps'. Never present a hypothesis as fact.",
            [ToolNames.SearchProject, ToolNames.ReadFile]),

        new(AgentType.ArchitectureAnalyst, "Architecture Analyst",
            "Summarize structure, technologies, and dependencies.",
            "You are Architecture Analyst. Use GetProjectStructure and read key files to produce an " +
            "architecture summary: main technologies, major modules/components, and how they depend on " +
            "each other. Ground every statement in files you inspected; cite paths.",
            AllTools),
    ];

    public static AgentDefinition For(AgentType type) =>
        All.FirstOrDefault(a => a.Type == type)
        ?? throw new NotFoundException($"Unknown agent type '{type}'.");
}
```

- [ ] **Step 2: Build** — `dotnet build src/DevDocsAI.Application/DevDocsAI.Application.csproj` — Expected: succeeded, 0 warnings.

- [ ] **Step 3: Commit**
```bash
git add src/DevDocsAI.Application/Features/Agents/AgentCatalog.cs
git commit -m "feat(app): built-in agent catalog (4 agents)"
```

---

## Task 6: `AgentService` (ReAct loop) + DTOs + repo port

**Files:** Create `src/DevDocsAI.Application/Features/Agents/AgentDtos.cs`, `src/DevDocsAI.Application/Features/Agents/AgentService.cs`; Modify `src/DevDocsAI.Application/Abstractions/Persistence/Repositories.cs`, `src/DevDocsAI.Application/DependencyInjection.cs`; Test `tests/DevDocsAI.UnitTests/Features/AgentServiceTests.cs`

- [ ] **Step 1: Add repo port** — in `src/DevDocsAI.Application/Abstractions/Persistence/Repositories.cs`, add (near `IConversationRepository`):
```csharp
public interface IAgentRunRepository
{
    Task<AgentRun?> GetWithToolExecutionsAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AgentRun>> ListByProjectAsync(Guid projectId, Guid userId, CancellationToken ct);
    Task AddAsync(AgentRun run, CancellationToken ct);
}
```

- [ ] **Step 2: DTOs** — `src/DevDocsAI.Application/Features/Agents/AgentDtos.cs`:
```csharp
namespace DevDocsAI.Application.Features.Agents;

public sealed record AgentRunRequest(string Input);

public sealed record AgentInfo(string Type, string DisplayName, string Description);

public sealed record TraceItem(
    int Sequence, string ToolName, string Input, string Output, string Status, string? Error, long DurationMs);

public sealed record AgentRunResponse(
    Guid Id,
    string AgentType,
    string Status,
    string? Output,
    string? Error,
    int Iterations,
    IReadOnlyList<TraceItem> Trace,
    DateTime CreatedAt);

public sealed record AgentRunSummary(
    Guid Id, string AgentType, string Status, int Iterations, DateTime CreatedAt);
```

- [ ] **Step 3: Write the failing test** — `tests/DevDocsAI.UnitTests/Features/AgentServiceTests.cs`:
```csharp
using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Agents;
using DevDocsAI.Application.Features.Agents.Tools;
using DevDocsAI.Application.Features.Rag;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class AgentServiceTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IAgentRunRepository _runs = Substitute.For<IAgentRunRepository>();
    private readonly IRetrievalService _retrieval = Substitute.For<IRetrievalService>();
    private readonly IChatCompletionService _chat = Substitute.For<IChatCompletionService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly AgentService _sut;

    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly Guid _projectId = Guid.CreateVersion7();

    public AgentServiceTests()
    {
        _retrieval.RetrieveAsync(_projectId, Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SearchHit>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "auth.cs", "src/auth.cs", 1, 5, 0.9, "class Auth {}"),
            });

        var registry = new ToolRegistry(new IAgentTool[]
        {
            new SearchProjectTool(_retrieval, Options.Create(new AgentOptions())),
        });

        _sut = new AgentService(_projects, _runs, registry, _chat, _uow, Options.Create(new AgentOptions()));
        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, _userId));
    }

    [Fact]
    public async Task Run_executes_a_tool_then_answers_and_records_the_trace()
    {
        // First model turn: call SearchProject. Second turn: final answer.
        _chat.CompleteAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new ChatCompletion("""{"action":{"tool":"SearchProject","arguments":{"query":"auth"}}}"""),
                new ChatCompletion("""{"final_answer":"Auth lives in src/auth.cs."}"""));

        var response = await _sut.RunAsync(
            _userId, _projectId, "CodeExplorer", new AgentRunRequest("where is auth?"), default);

        response.Status.ShouldBe(nameof(ProcessingStatus.Completed));
        response.Output.ShouldBe("Auth lives in src/auth.cs.");
        response.Trace.ShouldHaveSingleItem().ToolName.ShouldBe("SearchProject");
        response.Trace[0].Status.ShouldBe(nameof(ToolExecutionStatus.Ok));
        await _runs.Received(1).AddAsync(Arg.Any<AgentRun>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_answers_immediately_without_tools()
    {
        _chat.CompleteAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatCompletion("""{"final_answer":"Done."}"""));

        var response = await _sut.RunAsync(
            _userId, _projectId, "CodeExplorer", new AgentRunRequest("hi"), default);

        response.Status.ShouldBe(nameof(ProcessingStatus.Completed));
        response.Trace.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_records_an_error_when_a_tool_is_unknown()
    {
        _chat.CompleteAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new ChatCompletion("""{"action":{"tool":"Nope","arguments":{}}}"""),
                new ChatCompletion("""{"final_answer":"ok"}"""));

        var response = await _sut.RunAsync(
            _userId, _projectId, "CodeExplorer", new AgentRunRequest("q"), default);

        response.Trace.ShouldHaveSingleItem().Status.ShouldBe(nameof(ToolExecutionStatus.Error));
        response.Trace[0].ToolName.ShouldBe("Nope");
    }

    [Fact]
    public async Task Run_fails_after_max_iterations_without_a_final_answer()
    {
        var sut = new AgentService(_projects, _runs,
            new ToolRegistry(new IAgentTool[] { new SearchProjectTool(_retrieval, Options.Create(new AgentOptions())) }),
            _chat, _uow, Options.Create(new AgentOptions { MaxIterations = 2 }));
        _chat.CompleteAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatCompletion("not json at all"));

        var response = await sut.RunAsync(
            _userId, _projectId, "CodeExplorer", new AgentRunRequest("q"), default);

        response.Status.ShouldBe(nameof(ProcessingStatus.Failed));
        response.Iterations.ShouldBe(2);
    }

    [Fact]
    public async Task Run_on_another_users_project_is_not_found()
    {
        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, Guid.CreateVersion7()));

        await Should.ThrowAsync<NotFoundException>(() => _sut.RunAsync(
            _userId, _projectId, "CodeExplorer", new AgentRunRequest("q"), default));
    }

    [Fact]
    public async Task Run_with_unknown_agent_type_is_not_found()
    {
        await Should.ThrowAsync<NotFoundException>(() => _sut.RunAsync(
            _userId, _projectId, "Wizard", new AgentRunRequest("q"), default));
    }

    [Fact]
    public async Task Run_with_empty_input_is_a_validation_error()
    {
        await Should.ThrowAsync<ValidationException>(() => _sut.RunAsync(
            _userId, _projectId, "CodeExplorer", new AgentRunRequest("   "), default));
    }
}
```

- [ ] **Step 4: Run — expect FAIL** (compile): `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~AgentServiceTests"`

- [ ] **Step 5: Implement `AgentService`** — `src/DevDocsAI.Application/Features/Agents/AgentService.cs`:
```csharp
using System.Diagnostics;
using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Application.Features.Agents;

public interface IAgentService
{
    IReadOnlyList<AgentInfo> ListAgents();
    Task<AgentRunResponse> RunAsync(Guid userId, Guid projectId, string agentType, AgentRunRequest request, CancellationToken ct);
    Task<IReadOnlyList<AgentRunSummary>> ListRunsAsync(Guid userId, Guid projectId, CancellationToken ct);
    Task<AgentRunResponse> GetRunAsync(Guid userId, Guid projectId, Guid runId, CancellationToken ct);
}

/// <summary>
/// Runs a built-in agent as a bounded ReAct loop over the tool registry, persisting
/// the run and its tool-call trace. Synchronous: the answer and full trace are
/// returned when the loop finishes (final answer or iteration cap).
/// </summary>
public sealed class AgentService(
    IProjectRepository projects,
    IAgentRunRepository runs,
    ToolRegistry tools,
    IChatCompletionService chat,
    IUnitOfWork uow,
    IOptions<AgentOptions> options) : IAgentService
{
    private readonly AgentOptions _options = options.Value;

    private const string ReActContract =
        """
        Respond with EXACTLY ONE JSON object and nothing else — no prose, no code fences.
        To use a tool:
          {"thought": "<why>", "action": {"tool": "<ToolName>", "arguments": { ... }}}
        When you have enough information, give the final answer (Markdown allowed in the string):
          {"thought": "<why>", "final_answer": "<answer>"}
        Only use the tools listed above. After each tool call you will receive an "Observation".
        """;

    public IReadOnlyList<AgentInfo> ListAgents() =>
        AgentCatalog.All.Select(a => new AgentInfo(a.Type.ToString(), a.DisplayName, a.Description)).ToList();

    public async Task<AgentRunResponse> RunAsync(
        Guid userId, Guid projectId, string agentType, AgentRunRequest request, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["input"] = ["A non-empty input is required."],
            });
        }

        if (!Enum.TryParse<AgentType>(agentType, ignoreCase: true, out var type))
        {
            throw new NotFoundException($"Unknown agent type '{agentType}'.");
        }

        var def = AgentCatalog.For(type);
        var input = request.Input.Trim();
        var run = AgentRun.Start(projectId, userId, type, input);

        var systemPrompt =
            $"{def.SystemPrompt}\n\nYou can use these tools:\n{tools.Describe(def.Tools)}\n\n{ReActContract}";
        var messages = new List<ChatMessage> { new(ChatRole.User, input) };
        var sequence = 0;

        var iteration = 0;
        while (iteration < _options.MaxIterations)
        {
            iteration++;
            var completion = await chat.CompleteAsync(new ChatRequest(systemPrompt, messages), ct);
            var step = ReActParser.Parse(completion.Text);

            if (step is FinalStep final)
            {
                run.Complete(final.Answer, iteration);
                break;
            }

            messages.Add(new ChatMessage(ChatRole.Assistant, completion.Text));

            if (step is ActionStep action)
            {
                var (observation, status, error, durationMs) = await ExecuteToolAsync(projectId, def, action, ct);
                run.AddToolExecution(
                    ++sequence, action.Tool, action.Arguments.GetRawText(), observation, status, error, durationMs);
                messages.Add(new ChatMessage(ChatRole.User, $"Observation:\n{observation}"));
            }
            else
            {
                messages.Add(new ChatMessage(ChatRole.User,
                    "Your last response was not valid. Reply with a single JSON object containing " +
                    "either \"action\" or \"final_answer\"."));
            }
        }

        if (run.Status != ProcessingStatus.Completed)
        {
            run.Fail($"Stopped after {_options.MaxIterations} iterations without a final answer.", iteration);
        }

        await runs.AddAsync(run, ct);
        await uow.SaveChangesAsync(ct);
        return Map(run);
    }

    public async Task<IReadOnlyList<AgentRunSummary>> ListRunsAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        await EnsureProjectOwnedAsync(userId, projectId, ct);
        var list = await runs.ListByProjectAsync(projectId, userId, ct);
        return list
            .Select(r => new AgentRunSummary(r.Id, r.AgentType.ToString(), r.Status.ToString(), r.Iterations, r.CreatedAt))
            .ToList();
    }

    public async Task<AgentRunResponse> GetRunAsync(Guid userId, Guid projectId, Guid runId, CancellationToken ct)
    {
        var run = await runs.GetWithToolExecutionsAsync(runId, ct);
        if (run is null || run.ProjectId != projectId || run.UserId != userId)
        {
            throw new NotFoundException("Agent run not found.");
        }

        return Map(run);
    }

    private async Task<(string Observation, ToolExecutionStatus Status, string? Error, long DurationMs)>
        ExecuteToolAsync(Guid projectId, AgentDefinition def, ActionStep action, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var tool = tools.Resolve(action.Tool, def.Tools);
        if (tool is null)
        {
            return ($"Unknown tool '{action.Tool}'. Available tools: {string.Join(", ", def.Tools)}.",
                ToolExecutionStatus.Error, "unknown tool", sw.ElapsedMilliseconds);
        }

        try
        {
            var output = await tool.ExecuteAsync(projectId, action.Arguments, ct);
            return (output, ToolExecutionStatus.Ok, null, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ($"Tool error: {ex.Message}", ToolExecutionStatus.Error, ex.Message, sw.ElapsedMilliseconds);
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

    private static AgentRunResponse Map(AgentRun run) => new(
        run.Id,
        run.AgentType.ToString(),
        run.Status.ToString(),
        run.Output,
        run.Error,
        run.Iterations,
        run.ToolExecutions
            .OrderBy(t => t.Sequence)
            .Select(t => new TraceItem(
                t.Sequence, t.ToolName, t.InputJson, t.OutputJson, t.Status.ToString(), t.Error, t.DurationMs))
            .ToList(),
        run.CreatedAt);
}
```

- [ ] **Step 6: Register in DI** — in `src/DevDocsAI.Application/DependencyInjection.cs`, add (with the other `AddScoped`s) and ensure `using DevDocsAI.Application.Features.Agents;` + `using DevDocsAI.Application.Features.Agents.Tools;` are present:
```csharp
        services.AddScoped<IAgentTool, SearchProjectTool>();
        services.AddScoped<IAgentTool, ReadFileTool>();
        services.AddScoped<IAgentTool, GetProjectStructureTool>();
        services.AddScoped<ToolRegistry>();
        services.AddScoped<IAgentService, AgentService>();
```

- [ ] **Step 7: Run — expect PASS** (7 tests): `dotnet test tests/DevDocsAI.UnitTests/DevDocsAI.UnitTests.csproj --filter "FullyQualifiedName~AgentServiceTests"`

- [ ] **Step 8: Commit**
```bash
git add src/DevDocsAI.Application tests/DevDocsAI.UnitTests/Features/AgentServiceTests.cs
git commit -m "feat(app): AgentService ReAct orchestration loop + DTOs"
```

---

## Task 7: Persistence — configs, migration, repository, DI

**Files:** Create `src/DevDocsAI.Infrastructure/Persistence/Configurations/AgentRunConfiguration.cs`, `.../ToolExecutionConfiguration.cs`, `src/DevDocsAI.Infrastructure/Persistence/Repositories/AgentRunRepository.cs`; Modify `src/DevDocsAI.Infrastructure/Persistence/AppDbContext.cs`, `src/DevDocsAI.Infrastructure/DependencyInjection.cs`, `src/DevDocsAI.Api/appsettings.json`; migration `AddAgentRuns`.

- [ ] **Step 1: `AgentRun` config** — `src/DevDocsAI.Infrastructure/Persistence/Configurations/AgentRunConfiguration.cs`:
```csharp
using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevDocsAI.Infrastructure.Persistence.Configurations;

public sealed class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.ToTable("agent_runs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.AgentType).HasConversion<string>().HasMaxLength(64);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Input).IsRequired();
        builder.Property(r => r.Error).HasMaxLength(4000);

        builder.HasIndex(r => r.ProjectId);
        builder.HasIndex(r => new { r.ProjectId, r.UserId });

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(r => r.ToolExecutions)
            .WithOne()
            .HasForeignKey(t => t.AgentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(AgentRun.ToolExecutions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

- [ ] **Step 2: `ToolExecution` config** — `src/DevDocsAI.Infrastructure/Persistence/Configurations/ToolExecutionConfiguration.cs`:
```csharp
using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevDocsAI.Infrastructure.Persistence.Configurations;

public sealed class ToolExecutionConfiguration : IEntityTypeConfiguration<ToolExecution>
{
    public void Configure(EntityTypeBuilder<ToolExecution> builder)
    {
        builder.ToTable("tool_executions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.ToolName).HasMaxLength(128).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(t => t.InputJson).IsRequired();
        builder.Property(t => t.OutputJson).IsRequired();
        builder.Property(t => t.Error).HasMaxLength(4000);
        builder.HasIndex(t => t.AgentRunId);
    }
}
```

- [ ] **Step 3: DbSets** — in `src/DevDocsAI.Infrastructure/Persistence/AppDbContext.cs`, add:
```csharp
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<ToolExecution> ToolExecutions => Set<ToolExecution>();
```

- [ ] **Step 4: Repository** — `src/DevDocsAI.Infrastructure/Persistence/Repositories/AgentRunRepository.cs`:
```csharp
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocsAI.Infrastructure.Persistence.Repositories;

public sealed class AgentRunRepository(AppDbContext db) : IAgentRunRepository
{
    public Task<AgentRun?> GetWithToolExecutionsAsync(Guid id, CancellationToken ct) =>
        db.AgentRuns
            .Include(r => r.ToolExecutions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<AgentRun>> ListByProjectAsync(Guid projectId, Guid userId, CancellationToken ct) =>
        await db.AgentRuns
            .Where(r => r.ProjectId == projectId && r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(AgentRun run, CancellationToken ct) =>
        await db.AgentRuns.AddAsync(run, ct);
}
```

- [ ] **Step 5: Register repo + options** — in `src/DevDocsAI.Infrastructure/DependencyInjection.cs` add the repo with the others and bind options near the GitHub options:
```csharp
        services.AddScoped<IAgentRunRepository, AgentRunRepository>();
```
and:
```csharp
        services.AddOptions<AgentOptions>().Bind(configuration.GetSection(AgentOptions.SectionName));
```
Add `using DevDocsAI.Application.Features.Agents;` if not present.

- [ ] **Step 6: appsettings** — in `src/DevDocsAI.Api/appsettings.json`, add before `"AllowedHosts"`:
```json
  "Agent": {
    "MaxIterations": 8,
    "MaxFileChars": 8000,
    "SearchTopK": 6
  },
```

- [ ] **Step 7: Build + migration**
```bash
dotnet build src/DevDocsAI.Api/DevDocsAI.Api.csproj
dotnet ef migrations add AddAgentRuns --project src/DevDocsAI.Infrastructure --startup-project src/DevDocsAI.Api --output-dir Persistence/Migrations
```
Expected: build succeeded; migration "Done." Verify the migration creates `agent_runs` + `tool_executions` (FK cascade) and no other table changes.

- [ ] **Step 8: Commit**
```bash
git add src/DevDocsAI.Infrastructure src/DevDocsAI.Api/appsettings.json
git commit -m "feat(infra): persistence for agent runs + tool executions"
```

---

## Task 8: `AgentsController`

**Files:** Create `src/DevDocsAI.Api/Controllers/AgentsController.cs`

- [ ] **Step 1: Implement** — `src/DevDocsAI.Api/Controllers/AgentsController.cs`:
```csharp
using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevDocsAI.Api.Controllers;

/// <summary>Run the built-in AI agents over a project and review their tool traces (Phase 8).</summary>
[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/agents")]
public sealed class AgentsController(IAgentService agents, ICurrentUser currentUser) : ControllerBase
{
    private Guid UserId => currentUser.UserId
        ?? throw new AuthenticationException("The request is not authenticated.");

    [HttpGet]
    public ActionResult<IReadOnlyList<AgentInfo>> List() => Ok(agents.ListAgents());

    [HttpPost("{agentType}/run")]
    public async Task<ActionResult<AgentRunResponse>> Run(
        Guid projectId, string agentType, AgentRunRequest request, CancellationToken ct)
        => Ok(await agents.RunAsync(UserId, projectId, agentType, request, ct));

    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<AgentRunSummary>>> Runs(Guid projectId, CancellationToken ct)
        => Ok(await agents.ListRunsAsync(UserId, projectId, ct));

    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<AgentRunResponse>> GetRun(Guid projectId, Guid runId, CancellationToken ct)
        => Ok(await agents.GetRunAsync(UserId, projectId, runId, ct));
}
```

- [ ] **Step 2: Build** — `dotnet build src/DevDocsAI.Api/DevDocsAI.Api.csproj` — Expected: succeeded, 0 warnings.

- [ ] **Step 3: Commit**
```bash
git add src/DevDocsAI.Api/Controllers/AgentsController.cs
git commit -m "feat(api): agents controller (list/run/history)"
```

---

## Task 9: Integration tests

**Files:** Modify `tests/DevDocsAI.IntegrationTests/Infrastructure/FakeAiServices.cs`; Create `tests/DevDocsAI.IntegrationTests/AgentEndpointsTests.cs`

- [ ] **Step 1: Make the fake chat provider agent-aware** — in `tests/DevDocsAI.IntegrationTests/Infrastructure/FakeAiServices.cs`, replace the `FakeChatCompletionService.CompleteAsync` method body with:
```csharp
    public Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        // Agent requests use the ReAct contract (the system prompt contains "final_answer").
        // Drive one SearchProject tool call, then answer once an Observation is present.
        if (request.SystemPrompt.Contains("final_answer", StringComparison.Ordinal))
        {
            var hasObservation = request.Messages.Any(m => m.Content.StartsWith("Observation:", StringComparison.Ordinal));
            var json = hasObservation
                ? """{"final_answer":"Based on the project files, here is the analysis."}"""
                : """{"action":{"tool":"SearchProject","arguments":{"query":"registration"}}}""";
            return Task.FromResult(new ChatCompletion(json));
        }

        return Task.FromResult(new ChatCompletion(Answer));
    }
```
(Keep the existing `Answer` const and `StreamAsync`.)

- [ ] **Step 2: Write the integration tests** — `tests/DevDocsAI.IntegrationTests/AgentEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DevDocsAI.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AgentEndpointsTests(DevDocsApiFactory factory)
{
    private sealed record DocumentModel(Guid Id, string Status);
    private sealed record UploadResult(List<DocumentModel> Accepted);
    private sealed record TraceItem(int Sequence, string ToolName, string Status);
    private sealed record RunResponse(Guid Id, string AgentType, string Status, string? Output, int Iterations, List<TraceItem> Trace);
    private sealed record RunSummary(Guid Id, string AgentType, string Status);
    private sealed record AgentInfo(string Type, string DisplayName, string Description);

    private const string Fact = "user registration is handled in AuthController.Register";

    [Fact]
    public async Task Lists_the_four_built_in_agents()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var agents = await client.GetFromJsonAsync<List<AgentInfo>>(
            $"/api/v1/projects/{projectId}/agents");

        agents!.Count.ShouldBe(4);
        agents.ShouldContain(a => a.Type == "CodeExplorer");
        agents.ShouldContain(a => a.Type == "BugAnalysis");
    }

    [Fact]
    public async Task Run_uses_a_tool_answers_and_persists_the_trace()
    {
        var (client, projectId) = await IndexedProjectAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/agents/CodeExplorer/run",
            new { input = "where is user registration?" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var run = (await response.Content.ReadFromJsonAsync<RunResponse>())!;
        run.Status.ShouldBe("Completed");
        run.Output.ShouldNotBeNullOrWhiteSpace();
        run.Trace.ShouldContain(t => t.ToolName == "SearchProject" && t.Status == "Ok");

        // Re-viewable via history.
        var fetched = await client.GetFromJsonAsync<RunResponse>(
            $"/api/v1/projects/{projectId}/agents/runs/{run.Id}");
        fetched!.Trace.Count.ShouldBe(run.Trace.Count);

        var runs = await client.GetFromJsonAsync<List<RunSummary>>(
            $"/api/v1/projects/{projectId}/agents/runs");
        runs!.ShouldContain(r => r.Id == run.Id);
    }

    [Fact]
    public async Task Unknown_agent_type_is_not_found()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/agents/Wizard/run", new { input = "hi" });
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Empty_input_is_rejected()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/agents/CodeExplorer/run", new { input = "   " });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Another_user_cannot_run_or_read_runs()
    {
        var (owner, projectId) = await IndexedProjectAsync();
        var run = (await (await owner.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/agents/CodeExplorer/run", new { input = "q" }))
            .Content.ReadFromJsonAsync<RunResponse>())!;

        var (intruder, _, _) = await factory.RegisterAsync();

        (await intruder.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/agents/CodeExplorer/run", new { input = "q" }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.GetAsync(
            $"/api/v1/projects/{projectId}/agents/runs/{run.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<(HttpClient client, Guid projectId)> IndexedProjectAsync()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(Fact));
        part.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(part, "files", "notes.md");

        var upload = (await (await client.PostAsync($"/api/v1/projects/{projectId}/documents", form))
            .Content.ReadFromJsonAsync<UploadResult>())!;
        await WaitUntilProcessedAsync(client, projectId, upload.Accepted.Single().Id);
        return (client, projectId);
    }

    private static async Task WaitUntilProcessedAsync(HttpClient client, Guid projectId, Guid documentId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var docs = await client.GetFromJsonAsync<List<DocumentModel>>(
                $"/api/v1/projects/{projectId}/documents");
            var doc = docs!.Single(d => d.Id == documentId);
            if (doc.Status is "Completed" or "Failed") { doc.Status.ShouldBe("Completed"); return; }
            await Task.Delay(200);
        }

        throw new Xunit.Sdk.XunitException("Document was not processed in time.");
    }
}
```

- [ ] **Step 3: Run the agent integration tests** — `dotnet test tests/DevDocsAI.IntegrationTests/DevDocsAI.IntegrationTests.csproj --filter "FullyQualifiedName~AgentEndpointsTests"` — Expected: PASS (5 tests). (Docker running.)

- [ ] **Step 4: Full backend suite (no regressions)** — `dotnet test` — Expected: all pass, 0 warnings.

- [ ] **Step 5: Commit**
```bash
git add tests/DevDocsAI.IntegrationTests
git commit -m "test(int): agent run + trace + history + cross-tenant; agent-aware fake chat"
```

---

## Task 10: Frontend — Agents + Documentation tabs

**Files:** Modify `frontend/src/lib/types.ts`; Create `frontend/src/components/agent-trace.tsx`, `frontend/src/components/project-agents.tsx`, `frontend/src/components/project-documentation.tsx`; Modify `frontend/src/app/projects/[id]/page.tsx`

- [ ] **Step 1: Types** — in `frontend/src/lib/types.ts`, append:
```ts
export interface AgentInfo {
  type: string;
  displayName: string;
  description: string;
}

export interface TraceItem {
  sequence: number;
  toolName: string;
  input: string;
  output: string;
  status: string;
  error: string | null;
  durationMs: number;
}

export interface AgentRunResponse {
  id: string;
  agentType: string;
  status: string;
  output: string | null;
  error: string | null;
  iterations: number;
  trace: TraceItem[];
  createdAt: string;
}
```

- [ ] **Step 2: Trace view** — `frontend/src/components/agent-trace.tsx`:
```tsx
"use client";

import { useState } from "react";
import type { TraceItem } from "@/lib/types";

export function AgentTrace({ trace }: { trace: TraceItem[] }) {
  const [open, setOpen] = useState(false);
  if (trace.length === 0) return null;

  return (
    <div className="mt-4 rounded-lg border border-line bg-panel/40">
      <button
        onClick={() => setOpen((v) => !v)}
        className="flex w-full items-center justify-between px-4 py-2.5 text-left"
      >
        <span className="eyebrow">Tool trace · {trace.length} call{trace.length === 1 ? "" : "s"}</span>
        <span className="font-mono text-xs text-faint">{open ? "hide" : "show"}</span>
      </button>
      {open && (
        <ol className="space-y-3 border-t border-line px-4 py-3">
          {trace.map((t) => (
            <li key={t.sequence} className="text-xs">
              <div className="flex items-center gap-2">
                <span className="font-mono text-accent">{t.toolName}</span>
                <span
                  className={`font-mono text-[0.65rem] ${t.status === "Ok" ? "text-ok" : "text-danger"}`}
                >
                  {t.status}
                </span>
                <span className="font-mono text-[0.65rem] text-faint">{t.durationMs}ms</span>
              </div>
              <pre className="mt-1 overflow-x-auto whitespace-pre-wrap font-mono text-[0.7rem] text-muted">
                input: {t.input}
              </pre>
              <pre className="mt-1 max-h-40 overflow-auto whitespace-pre-wrap font-mono text-[0.7rem] text-muted">
                {t.output}
              </pre>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Agents panel** — `frontend/src/components/project-agents.tsx`:
```tsx
"use client";

import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { AgentTrace } from "@/components/agent-trace";
import { Markdown } from "@/components/Markdown";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { AgentRunResponse } from "@/lib/types";

const AGENTS = [
  { type: "CodeExplorer", label: "Code Explorer", hint: "Ask where something is implemented." },
  { type: "BugAnalysis", label: "Bug Analysis", hint: "Paste an error + stack trace." },
  { type: "ArchitectureAnalyst", label: "Architecture Analyst", hint: "Summarize the architecture." },
] as const;

export function AgentsPanel({ projectId }: { projectId: string }) {
  const { authFetch } = useAuth();
  const [agent, setAgent] = useState<(typeof AGENTS)[number]["type"]>("CodeExplorer");
  const [input, setInput] = useState("");
  const [error, setError] = useState("");
  const [stack, setStack] = useState("");

  const run = useMutation({
    mutationFn: (body: string) =>
      authFetch<AgentRunResponse>(`/api/v1/projects/${projectId}/agents/${agent}/run`, {
        method: "POST",
        body: { input: body },
      }),
  });

  const isBug = agent === "BugAnalysis";
  const composed = isBug
    ? [error && `Error: ${error}`, stack && `Stack trace:\n${stack}`, input && `Notes: ${input}`]
        .filter(Boolean)
        .join("\n\n")
    : input.trim();

  return (
    <div>
      <div className="flex flex-wrap gap-1 border-b border-line pb-3">
        {AGENTS.map((a) => (
          <button
            key={a.type}
            onClick={() => {
              setAgent(a.type);
              run.reset();
            }}
            className={`rounded-md px-3 py-1.5 text-sm transition-colors ${
              agent === a.type ? "bg-panel text-ink" : "text-muted hover:text-ink"
            }`}
          >
            {a.label}
          </button>
        ))}
      </div>

      <form
        className="mt-4 flex flex-col gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          if (composed) run.mutate(composed);
        }}
      >
        <p className="text-xs text-muted">{AGENTS.find((a) => a.type === agent)!.hint}</p>
        {isBug && (
          <>
            <input
              value={error}
              onChange={(e) => setError(e.target.value)}
              placeholder="Error message"
              className="h-10 w-full rounded-md border border-line bg-panel px-3 text-sm text-ink placeholder:text-faint focus:border-accent focus:outline-none"
            />
            <textarea
              value={stack}
              onChange={(e) => setStack(e.target.value)}
              rows={3}
              placeholder="Stack trace (optional)"
              className="w-full rounded-md border border-line bg-panel px-3 py-2 font-mono text-xs text-ink placeholder:text-faint focus:border-accent focus:outline-none"
            />
          </>
        )}
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          rows={isBug ? 2 : 3}
          placeholder={isBug ? "Extra context (optional)" : "Ask the agent…"}
          className="w-full rounded-md border border-line bg-panel px-3 py-2 text-sm text-ink placeholder:text-faint focus:border-accent focus:outline-none"
        />
        <div>
          <Button type="submit" disabled={run.isPending || !composed}>
            {run.isPending ? <Spinner /> : "Run agent"}
          </Button>
        </div>
      </form>

      {run.isPending && (
        <p className="mt-4 text-sm text-muted">The agent is working — this can take a moment on a local model…</p>
      )}
      {run.isError && (
        <p className="mt-4 text-sm text-danger">
          {run.error instanceof ApiError ? run.error.message : "The agent run failed."}
        </p>
      )}
      {run.data && (
        <div className="mt-5">
          {run.data.status === "Failed" ? (
            <p className="text-sm text-danger">{run.data.error ?? "The agent could not complete."}</p>
          ) : (
            <Markdown content={run.data.output ?? ""} />
          )}
          <AgentTrace trace={run.data.trace} />
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Documentation panel** — `frontend/src/components/project-documentation.tsx`:
```tsx
"use client";

import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { AgentTrace } from "@/components/agent-trace";
import { Markdown } from "@/components/Markdown";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/field";
import { Spinner } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { AgentRunResponse } from "@/lib/types";

export function DocumentationPanel({ projectId }: { projectId: string }) {
  const { authFetch } = useAuth();
  const [topic, setTopic] = useState("");

  const run = useMutation({
    mutationFn: () =>
      authFetch<AgentRunResponse>(`/api/v1/projects/${projectId}/agents/DocumentationGenerator/run`, {
        method: "POST",
        body: { input: topic.trim() },
      }),
  });

  return (
    <div>
      <form
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          if (topic.trim()) run.mutate();
        }}
      >
        <Input
          value={topic}
          onChange={(e) => setTopic(e.target.value)}
          placeholder="What should I document? e.g. 'the authentication flow'"
          aria-label="Documentation topic"
        />
        <Button type="submit" disabled={run.isPending || !topic.trim()}>
          {run.isPending ? <Spinner /> : "Generate"}
        </Button>
      </form>

      {run.isPending && (
        <p className="mt-4 text-sm text-muted">Reading the source and writing docs…</p>
      )}
      {run.isError && (
        <p className="mt-4 text-sm text-danger">
          {run.error instanceof ApiError ? run.error.message : "Generation failed."}
        </p>
      )}
      {run.data && (
        <div className="mt-5 rounded-xl border border-line bg-panel/30 p-5">
          {run.data.status === "Failed" ? (
            <p className="text-sm text-danger">{run.data.error ?? "Could not generate documentation."}</p>
          ) : (
            <Markdown content={run.data.output ?? ""} />
          )}
          <AgentTrace trace={run.data.trace} />
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 5: Wire the tabs** — in `frontend/src/app/projects/[id]/page.tsx`:

Add imports near the other component imports:
```tsx
import { AgentsPanel } from "@/components/project-agents";
import { DocumentationPanel } from "@/components/project-documentation";
```
Change the `TABS` array so `agents` and `docs` no longer carry `phase`:
```tsx
const TABS = [
  { key: "overview", label: "Overview" },
  { key: "chat", label: "Chat" },
  { key: "search", label: "Search" },
  { key: "agents", label: "Agents" },
  { key: "docs", label: "Documentation" },
] as const;
```
Replace the tab-content conditional block with:
```tsx
          <div className="mt-6">
            {tab === "overview" ? (
              <Overview project={project.data} />
            ) : tab === "chat" ? (
              <ChatPanel projectId={project.data.id} />
            ) : tab === "search" ? (
              <SearchPanel projectId={project.data.id} />
            ) : tab === "agents" ? (
              <AgentsPanel projectId={project.data.id} />
            ) : (
              <DocumentationPanel projectId={project.data.id} />
            )}
          </div>
```
(The `ComingSoon` component is now unused — delete its definition and the now-unused `phase` references in the tab-button JSX: the `{"phase" in t && (…)}` block.)

- [ ] **Step 6: Typecheck, lint, build** — from `frontend/`:
```bash
npx tsc --noEmit && npm run lint && npm run build
```
Expected: tsc exit 0, lint clean, "Compiled successfully".

- [ ] **Step 7: Commit**
```bash
git add frontend
git commit -m "feat(web): Agents + Documentation tabs with tool-trace view"
```

---

## Task 11: Wrap-up — verify + docs

**Files:** Modify `IMPLEMENTATION_PLAN.md`, `memory/devdocs-ai-status.md`

- [ ] **Step 1: Full backend gates** — from `backend/`: `dotnet build && dotnet test` — Expected: 0 warnings; all unit + integration pass.
- [ ] **Step 2: Frontend gates** — from `frontend/`: `npx tsc --noEmit && npm run lint && npm run build` — all clean.
- [ ] **Step 3: (Optional) live smoke test** — restart the API (applies `AddAgentRuns`), open the Agents tab on an indexed project, run Code Explorer with "where is X implemented?", confirm an answer + a SearchProject trace entry; try the Documentation tab.
- [ ] **Step 4: Update docs** — set `IMPLEMENTATION_PLAN.md` status to "Phase 8 complete → next Phase 9 (Production quality)", add a Phase 8 bullet with the new test count; update `memory/devdocs-ai-status.md`.
- [ ] **Step 5: Commit + merge**
```bash
git add IMPLEMENTATION_PLAN.md
git commit -m "docs: mark Phase 8 (agents & tools) complete"
git switch main && git merge --no-ff phase-8-agents -m "Merge Phase 8: agents & tools"
```

---

## Self-review notes (author)

- **Spec coverage:** ReAct loop (Task 6) · 3 tools SearchProject/ReadFile/GetProjectStructure (Task 4) · 4 agents with personas incl. Bug Analysis evidence-vs-hypothesis prompt (Task 5) · persisted AgentRun + ToolExecution trace / observability (Tasks 1,7) · synchronous run + trace API (Tasks 6,8) · Agents + Documentation tabs (Task 10) · ownership + caps + validation (Task 6) · agent-aware fake for net-free tests (Task 9). Milestone "four agents usable end-to-end" — the four are listed by `GET /agents` (Task 9 asserts 4) and runnable via `POST /{type}/run`.
- **Type consistency:** `AgentStep`/`FinalStep`/`ActionStep`/`UnparseableStep`, `IAgentTool.ExecuteAsync(projectId, JsonElement, ct)`, `ToolNames.*`, `ToolRegistry.Resolve/Describe`, `AgentDefinition(Type,DisplayName,Description,SystemPrompt,Tools)`, `AgentRun.Complete(output,iterations)/Fail(error,iterations)/AddToolExecution(seq,name,inputJson,outputJson,status,error,ms)`, `IAgentRunRepository.GetWithToolExecutionsAsync/ListByProjectAsync/AddAsync`, and DTOs are used identically across tasks.
- **No placeholders:** every step has concrete code/commands.
- **Deferred (not in scope):** native function-calling; streaming agent steps; SearchFiles/FindReferences/GetDocument tools; user-defined agents. Documentation Generator is reached from its own tab (not the Agents picker) by design.
