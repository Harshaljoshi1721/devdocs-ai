namespace DevDocsAI.Infrastructure.AI;

/// <summary>Google Gemini configuration, bound from the "Gemini" section.</summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>Free API key from Google AI Studio. Empty until configured; calls fail clearly if unset.</summary>
    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta/";

    public string EmbeddingModel { get; init; } = "gemini-embedding-001";
    public int EmbeddingDimensions { get; init; } = 768;

    public string ChatModel { get; init; } = "gemini-2.0-flash";
}
