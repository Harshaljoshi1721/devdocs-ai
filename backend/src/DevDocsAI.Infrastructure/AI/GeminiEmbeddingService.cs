using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DevDocsAI.Application.Abstractions.AI;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Infrastructure.AI;

/// <summary>Embeddings via Google Gemini (batchEmbedContents), batched to the API limit.</summary>
public sealed class GeminiEmbeddingService(HttpClient http, IOptions<GeminiOptions> options) : IEmbeddingService
{
    private const int BatchLimit = 100;
    private readonly GeminiOptions _options = options.Value;

    public int Dimensions => _options.EmbeddingDimensions;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        EnsureConfigured();
        if (texts.Count == 0)
        {
            return [];
        }

        var results = new List<float[]>(texts.Count);
        var modelPath = $"models/{_options.EmbeddingModel}";

        foreach (var batch in Batches(texts, BatchLimit))
        {
            var request = new BatchEmbedRequest(
                batch.Select(t => new EmbedRequest(
                    modelPath, new Content([new Part(t)]), _options.EmbeddingDimensions)).ToList());

            using var message = new HttpRequestMessage(
                HttpMethod.Post, $"{_options.BaseUrl}{modelPath}:batchEmbedContents");
            message.Headers.Add("x-goog-api-key", _options.ApiKey);
            message.Content = JsonContent.Create(request);

            using var response = await http.SendAsync(message, ct);
            await EnsureSuccessAsync(response, ct);

            var body = await response.Content.ReadFromJsonAsync<BatchEmbedResponse>(ct)
                ?? throw new InvalidOperationException("Empty embedding response from Gemini.");
            results.AddRange(body.Embeddings.Select(e => e.Values));
        }

        return results;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured (set Gemini:ApiKey). Required to generate embeddings.");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Gemini embedding request failed ({(int)response.StatusCode}): {detail}");
        }
    }

    private static IEnumerable<IReadOnlyList<string>> Batches(IReadOnlyList<string> items, int size)
    {
        for (var i = 0; i < items.Count; i += size)
        {
            yield return items.Skip(i).Take(size).ToList();
        }
    }

    private sealed record BatchEmbedRequest([property: JsonPropertyName("requests")] List<EmbedRequest> Requests);
    private sealed record EmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("content")] Content Content,
        [property: JsonPropertyName("outputDimensionality")] int OutputDimensionality);
    private sealed record Content([property: JsonPropertyName("parts")] List<Part> Parts);
    private sealed record Part([property: JsonPropertyName("text")] string Text);

    private sealed record BatchEmbedResponse([property: JsonPropertyName("embeddings")] List<Embedding> Embeddings);
    private sealed record Embedding([property: JsonPropertyName("values")] float[] Values);
}
