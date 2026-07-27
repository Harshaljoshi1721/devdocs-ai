using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevDocsAI.Application.Abstractions.AI;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Infrastructure.AI;

/// <summary>Chat completions via a local Ollama server (/api/chat). Free, offline.</summary>
public sealed class OllamaChatCompletionService(HttpClient http, IOptions<OllamaOptions> options)
    : IChatCompletionService
{
    private readonly OllamaOptions _options = options.Value;

    public async Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        var payload = new ChatBody(_options.Model, BuildMessages(request), Stream: false);

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(Endpoint, payload, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw Unreachable(ex);
        }

        await EnsureSuccessAsync(response, ct);

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>(ct);
        return new ChatCompletion(body?.Message?.Content ?? "The model did not return a response.");
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var payload = new ChatBody(_options.Model, BuildMessages(request), Stream: true);
        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(payload),
        };

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw Unreachable(ex);
        }

        using (response)
        {
            await EnsureSuccessAsync(response, ct);

            // Ollama streams newline-delimited JSON objects, each with a message delta.
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var chunk = JsonSerializer.Deserialize<ChatResponse>(line);
                var delta = chunk?.Message?.Content;
                if (!string.IsNullOrEmpty(delta))
                {
                    yield return delta;
                }
            }
        }
    }

    private string Endpoint => $"{_options.BaseUrl.TrimEnd('/')}/api/chat";

    private List<Message> BuildMessages(ChatRequest request)
    {
        var messages = new List<Message> { new("system", request.SystemPrompt) };
        messages.AddRange(request.Messages.Select(m =>
            new Message(m.Role == ChatRole.Assistant ? "assistant" : "user", m.Content)));
        return messages;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Ollama chat request failed ({(int)response.StatusCode}): {detail}");
        }
    }

    private InvalidOperationException Unreachable(Exception ex) => new(
        $"Could not reach Ollama at {_options.BaseUrl}. Is it running (`ollama serve`) and the model " +
        $"pulled (`ollama pull {_options.Model}`)?", ex);

    private sealed record ChatBody(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] List<Message> Messages,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record Message(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatResponse([property: JsonPropertyName("message")] Message? Message);
}
