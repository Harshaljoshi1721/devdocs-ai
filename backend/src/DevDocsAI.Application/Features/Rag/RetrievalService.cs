using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Application.Abstractions.Persistence;

namespace DevDocsAI.Application.Features.Rag;

/// <summary>Embeds a query and returns the most relevant project-scoped chunks as located, scored hits.</summary>
public interface IRetrievalService
{
    Task<IReadOnlyList<SearchHit>> RetrieveAsync(
        Guid projectId, string query, int? topK, CancellationToken ct);
}

/// <summary>
/// The retrieval half of RAG, shared by one-shot search/ask and multi-turn chat:
/// embed the query, cosine-search the project's vectors, rerank, and hydrate the
/// matches with their source document + line range.
/// </summary>
public sealed class RetrievalService(
    IEmbeddingService embeddings,
    IVectorStore vectorStore,
    IReranker reranker,
    IDocumentChunkRepository chunks,
    IDocumentRepository documents) : IRetrievalService
{
    private const int DefaultTopK = 8;
    private const int SnippetLength = 400;

    public async Task<IReadOnlyList<SearchHit>> RetrieveAsync(
        Guid projectId, string query, int? topK, CancellationToken ct)
    {
        var k = Math.Clamp(topK ?? DefaultTopK, 1, 25);

        var queryVector = await embeddings.EmbedSingleAsync(query, ct);
        var rawHits = await vectorStore.SearchAsync(projectId, queryVector, k, ct);
        if (rawHits.Count == 0)
        {
            return [];
        }

        var ranked = await reranker.RerankAsync(query, rawHits, ct);
        var scoreByChunk = ranked.ToDictionary(h => h.ChunkId, h => h.Score);

        var chunkList = await chunks.ListByIdsAsync(scoreByChunk.Keys.ToList(), ct);
        var docList = await documents.ListByIdsAsync(
            chunkList.Select(c => c.DocumentId).Distinct().ToList(), ct);
        var docsById = docList.ToDictionary(d => d.Id);

        return chunkList
            .Where(c => docsById.ContainsKey(c.DocumentId))
            .Select(c =>
            {
                var doc = docsById[c.DocumentId];
                return new SearchHit(
                    c.Id, doc.Id, doc.Name, doc.Path, c.StartLine, c.EndLine,
                    scoreByChunk[c.Id], Snippet(c.Content));
            })
            .OrderByDescending(h => h.Score)
            .ToList();
    }

    private static string Snippet(string content) =>
        content.Length <= SnippetLength ? content : content[..SnippetLength] + "…";
}
