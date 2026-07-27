namespace DevDocsAI.Application.Abstractions.AI;

/// <summary>
/// Optionally reorders retrieved candidates for relevance. The default is a
/// pass-through (keeps vector-search order); a model-based reranker can be
/// dropped in later.
/// </summary>
public interface IReranker
{
    Task<IReadOnlyList<VectorSearchHit>> RerankAsync(
        string query, IReadOnlyList<VectorSearchHit> candidates, CancellationToken ct);
}

/// <summary>No-op reranker: returns candidates unchanged.</summary>
public sealed class PassthroughReranker : IReranker
{
    public Task<IReadOnlyList<VectorSearchHit>> RerankAsync(
        string query, IReadOnlyList<VectorSearchHit> candidates, CancellationToken ct) =>
        Task.FromResult(candidates);
}
