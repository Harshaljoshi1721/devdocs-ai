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
