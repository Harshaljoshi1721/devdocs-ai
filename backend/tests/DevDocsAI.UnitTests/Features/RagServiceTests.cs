using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Rag;
using DevDocsAI.Application.Features.Usage;
using DevDocsAI.Domain.Entities;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class RagServiceTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IRetrievalService _retrieval = Substitute.For<IRetrievalService>();
    private readonly IChatCompletionService _chat = Substitute.For<IChatCompletionService>();
    private readonly IUsageRecorder _usage = Substitute.For<IUsageRecorder>();
    private readonly RagService _sut;

    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly Guid _projectId = Guid.CreateVersion7();
    private readonly SearchHit _hit;

    public RagServiceTests()
    {
        _sut = new RagService(_projects, _retrieval, _chat, _usage);

        _hit = new SearchHit(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "auth.cs", "src/auth.cs",
            10, 25, 0.9, "public class AuthController {}");

        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, _userId)); // owner = _userId
    }

    private void GivenOneHit() =>
        _retrieval.RetrieveAsync(_projectId, Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SearchHit> { _hit });

    [Fact]
    public async Task Search_returns_the_retrieved_hits()
    {
        GivenOneHit();

        var response = await _sut.SearchAsync(_userId, _projectId, new SearchRequest("auth", null), default);

        var hit = response.Results.ShouldHaveSingleItem();
        hit.Path.ShouldBe("src/auth.cs");
        hit.StartLine.ShouldBe(10);
        hit.EndLine.ShouldBe(25);
        hit.Score.ShouldBe(0.9);
    }

    [Fact]
    public async Task Ask_with_context_returns_grounded_answer_with_citations()
    {
        GivenOneHit();
        _chat.CompleteAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChatCompletion("Authentication uses JWT tokens."));

        var response = await _sut.AskAsync(_userId, _projectId, new AskRequest("How does auth work?", null), default);

        response.Grounded.ShouldBeTrue();
        response.Answer.ShouldBe("Authentication uses JWT tokens.");
        response.Sources.ShouldHaveSingleItem().Path.ShouldBe("src/auth.cs");
    }

    [Fact]
    public async Task Ask_with_no_results_is_not_grounded_and_skips_the_model()
    {
        _retrieval.RetrieveAsync(_projectId, Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SearchHit>());

        var response = await _sut.AskAsync(_userId, _projectId, new AskRequest("anything", null), default);

        response.Grounded.ShouldBeFalse();
        response.Sources.ShouldBeEmpty();
        await _chat.DidNotReceive().CompleteAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_on_another_users_project_is_not_found()
    {
        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, Guid.CreateVersion7())); // owner = someone else

        await Should.ThrowAsync<NotFoundException>(
            () => _sut.SearchAsync(_userId, _projectId, new SearchRequest("x", null), default));
    }

    [Fact]
    public async Task Empty_query_is_a_validation_error()
    {
        await Should.ThrowAsync<ValidationException>(
            () => _sut.SearchAsync(_userId, _projectId, new SearchRequest("  ", null), default));
    }
}
