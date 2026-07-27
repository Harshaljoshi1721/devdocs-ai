namespace DevDocsAI.Infrastructure.AI;

/// <summary>Local Ollama configuration, bound from the "Ollama" section.</summary>
public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    /// <summary>Ollama server URL. Local dev: http://localhost:11434. In Docker: http://host.docker.internal:11434.</summary>
    public string BaseUrl { get; init; } = "http://localhost:11434";

    /// <summary>Model tag to use for chat (must be pulled: `ollama pull &lt;model&gt;`).</summary>
    public string Model { get; init; } = "llama3.2";
}
