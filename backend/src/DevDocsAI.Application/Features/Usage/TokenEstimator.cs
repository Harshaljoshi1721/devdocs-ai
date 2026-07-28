namespace DevDocsAI.Application.Features.Usage;

/// <summary>Rough token estimate (~4 chars/token). Provider-agnostic; exact counts are a future refinement.</summary>
public static class TokenEstimator
{
    public static int Estimate(string? text) => string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}
