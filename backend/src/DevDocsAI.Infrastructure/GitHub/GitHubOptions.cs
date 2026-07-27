namespace DevDocsAI.Infrastructure.GitHub;

/// <summary>GitHub endpoints for public repo download, bound from the "GitHub" section. No secrets.</summary>
public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>Codeload host that serves repository tarballs.</summary>
    public string CodeloadBaseUrl { get; init; } = "https://codeload.github.com/";

    /// <summary>REST API base (used to resolve the default branch / commit sha).</summary>
    public string ApiBaseUrl { get; init; } = "https://api.github.com/";

    public int TimeoutSeconds { get; init; } = 100;
}
