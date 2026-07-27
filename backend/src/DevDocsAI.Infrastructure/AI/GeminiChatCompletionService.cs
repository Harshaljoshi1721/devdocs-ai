using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevDocsAI.Application.Abstractions.AI;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Infrastructure.AI;

/// <summary>Chat completions via Google Gemini (generateContent / streamGenerateContent).</summary>
public sealed class GeminiChatCompletionService(HttpClient http, IOptions<GeminiOptions> options)
    : IChatCompletionService
{
    private readonly GeminiOptions _options = options.Value;

    public async Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        using var message = BuildRequest(request, "generateContent");
        using var response = await http.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);

        var body = await response.Content.ReadFromJsonAsync<GenerateResponse>(ct);
        return new ChatCompletion(ExtractText(body) ?? "The model did not return a response.");
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        // alt=sse makes Gemini emit Server-Sent Events (one `data:` line per chunk).
        using var message = BuildRequest(request, "streamGenerateContent?alt=sse");
        using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureSuccessAsync(response, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var json = line["data:".Length..].Trim();
            if (json.Length == 0)
            {
                continue;
            }

            var delta = ExtractText(JsonSerializer.Deserialize<GenerateResponse>(json));
            if (!string.IsNullOrEmpty(delta))
            {
                yield return delta;
            }
        }
    }

    private HttpRequestMessage BuildRequest(ChatRequest request, string method)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured (set Gemini:ApiKey). Required to generate answers.");
        }

        var payload = new GenerateRequest(
            new Content([new Part(request.SystemPrompt)]),
            request.Messages
                .Select(m => new Content(
                    [new Part(m.Content)],
                    m.Role == ChatRole.Assistant ? "model" : "user"))
                .ToList());

        var message = new HttpRequestMessage(
            HttpMethod.Post, $"{_options.BaseUrl}models/{_options.ChatModel}:{method}");
        message.Headers.Add("x-goog-api-key", _options.ApiKey);
        message.Content = JsonContent.Create(payload);
        return message;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Gemini chat request failed ({(int)response.StatusCode}): {detail}");
        }
    }

    private static string? ExtractText(GenerateResponse? body) =>
        body?.Candidates?
            .FirstOrDefault()?.Content?.Parts?
            .FirstOrDefault(p => !string.IsNullOrEmpty(p.Text))?.Text;

    private sealed record GenerateRequest(
        [property: JsonPropertyName("systemInstruction")] Content SystemInstruction,
        [property: JsonPropertyName("contents")] List<Content> Contents);

    private sealed record Content(
        [property: JsonPropertyName("parts")] List<Part> Parts,
        [property: JsonPropertyName("role")] string? Role = null);

    private sealed record Part([property: JsonPropertyName("text")] string Text);

    private sealed record GenerateResponse(
        [property: JsonPropertyName("candidates")] List<Candidate>? Candidates);
    private sealed record Candidate([property: JsonPropertyName("content")] Content? Content);
}
