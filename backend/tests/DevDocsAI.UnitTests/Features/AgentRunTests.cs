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
