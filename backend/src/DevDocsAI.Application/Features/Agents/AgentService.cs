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
