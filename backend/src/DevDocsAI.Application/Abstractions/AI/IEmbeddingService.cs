namespace DevDocsAI.Application.Abstractions.AI;

/// <summary>Generates embedding vectors for text. Provider is swappable behind this port.</summary>
public interface IEmbeddingService
{
    /// <summary>Dimensionality of the vectors produced (must match the vector store column).</summary>
    int Dimensions { get; }

    /// <summary>Embeds a batch of texts, returning one vector per input in order.</summary>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct);
}

public static class EmbeddingServiceExtensions
{
    public static async Task<float[]> EmbedSingleAsync(
        this IEmbeddingService service, string text, CancellationToken ct)
    {
        var result = await service.EmbedAsync([text], ct);
        return result[0];
    }
}
