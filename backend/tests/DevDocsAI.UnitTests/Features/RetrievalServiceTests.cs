using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Features.Rag;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class RetrievalServiceTests
{
    private readonly IDocumentChunkRepository _chunks = Substitute.For<IDocumentChunkRepository>();
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IEmbeddingService _embeddings = Substitute.For<IEmbeddingService>();
    private readonly IVectorStore _vectorStore = Substitute.For<IVectorStore>();
    private readonly RetrievalService _sut;

    private readonly Guid _projectId = Guid.CreateVersion7();
    private readonly Document _doc;
    private readonly DocumentChunk _chunk;

    public RetrievalServiceTests()
    {
        _sut = new RetrievalService(_embeddings, _vectorStore, new PassthroughReranker(), _chunks, _documents);

        _doc = new Document(_projectId, "auth.cs", "src/auth.cs", FileType.Code, "hash", 20, "key");
        _chunk = new DocumentChunk(_doc.Id, 0, "public class AuthController {}", 10, 25);

        _embeddings.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<float[]> { new float[768] });
    }

    [Fact]
    public async Task Retrieve_maps_hits_to_sources_with_line_ranges_and_score()
    {
        _vectorStore.SearchAsync(_projectId, Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<VectorSearchHit> { new(_chunk.Id, 0.9) });
        _chunks.ListByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<DocumentChunk> { _chunk });
        _documents.ListByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Document> { _doc });

        var hits = await _sut.RetrieveAsync(_projectId, "auth", null, default);

        var hit = hits.ShouldHaveSingleItem();
        hit.Path.ShouldBe("src/auth.cs");
        hit.DocumentName.ShouldBe("auth.cs");
        hit.StartLine.ShouldBe(10);
        hit.EndLine.ShouldBe(25);
        hit.Score.ShouldBe(0.9);
        hit.Snippet.ShouldContain("AuthController");
    }

    [Fact]
    public async Task Retrieve_returns_empty_when_the_vector_store_has_no_matches()
    {
        _vectorStore.SearchAsync(_projectId, Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<VectorSearchHit>());

        var hits = await _sut.RetrieveAsync(_projectId, "auth", null, default);

        hits.ShouldBeEmpty();
    }
}
