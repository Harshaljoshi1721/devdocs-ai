namespace DevDocsAI.Api.Configuration;

/// <summary>Rate-limit budgets per fixed window, bound from the "RateLimit" section.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public int AuthPermitPerWindow { get; init; } = 10;
    public int AiPermitPerWindow { get; init; } = 20;
    public int GlobalPermitPerWindow { get; init; } = 100;
    public int WindowSeconds { get; init; } = 60;
}
