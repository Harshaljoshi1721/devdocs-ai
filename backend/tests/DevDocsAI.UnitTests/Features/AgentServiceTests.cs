using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Agents;
using DevDocsAI.Application.Features.Agents.Tools;
using DevDocsAI.Application.Features.Rag;
using DevDocsAI.Application.Features.Usage;
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
    private readonly IUsageRecorder _usage = Substitute.For<IUsageRecorder>();
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

        _sut = new AgentService(_projects, _runs, registry, _chat, _uow, Options.Create(new AgentOptions()), _usage);
        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, _userId));
    }

    [Fact]
    public async Task Run_executes_a_tool_then_answers_and_records_the_trace()
    {
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
            _chat, _uow, Options.Create(new AgentOptions { MaxIterations = 2 }), _usage);
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
