using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using DevDocsAI.Application.Abstractions.AI;

namespace DevDocsAI.IntegrationTests.Infrastructure;

/// <summary>
/// Deterministic 768-dim embeddings for tests — identical text yields an
/// identical vector, so an exact-match query ranks that chunk first. No network.
/// </summary>
public sealed class FakeEmbeddingService : IEmbeddingService
{
    public int Dimensions => 768;

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        IReadOnlyList<float[]> vectors = texts.Select(Embed).ToList();
        return Task.FromResult(vectors);
    }

    private static float[] Embed(string text)
    {
        var seed = BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0);
        var rng = new Random(seed);
        var vector = new float[768];
        double norm = 0;
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(rng.NextDouble() * 2 - 1);
            norm += vector[i] * vector[i];
        }

        norm = Math.Sqrt(norm);
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / norm);
        }

        return vector;
    }
}

/// <summary>Canned chat completion that echoes it used the provided context.</summary>
public sealed class FakeChatCompletionService : IChatCompletionService
{
    private const string Answer = "Based on the provided project context, here is the answer.";

    public Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken ct) =>
        Task.FromResult(new ChatCompletion(Answer));

    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        // Emit the canned answer word-by-word so streaming tests see multiple deltas.
        foreach (var word in Answer.Split(' '))
        {
            ct.ThrowIfCancellationRequested();
            yield return word + " ";
            await Task.Yield();
        }
    }
}
