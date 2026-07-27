using System.Diagnostics.CodeAnalysis;

namespace DevDocsAI.Application.Features.Repositories;

public sealed record GitHubRepoRef(string Owner, string Repo, string? Ref);

/// <summary>
/// Parses and validates a public GitHub HTTPS repository URL. Only github.com is
/// accepted (SSRF guard); SSH, other hosts, and embedded credentials are rejected.
/// </summary>
public static class GitHubUrlParser
{
    private static readonly HashSet<string> AllowedHosts =
        new(StringComparer.OrdinalIgnoreCase) { "github.com", "www.github.com" };

    public static bool TryParse(string? url, [NotNullWhen(true)] out GitHubRepoRef? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(url)) return false;

        var trimmed = url.Trim();
        // Reject path traversal before Uri normalization collapses "../" segments.
        // GitHub owner/repo/ref names never legitimately contain "..".
        if (trimmed.Contains("..")) return false;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return false;

        if (uri.Scheme != Uri.UriSchemeHttps) return false;
        if (!AllowedHosts.Contains(uri.Host)) return false;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return false; // no user:pass@

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2) return false;

        var owner = segments[0];
        var repo = segments[1];
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            repo = repo[..^4];

        if (!IsSegment(owner) || !IsSegment(repo)) return false;

        string? @ref = null;
        // .../tree/{ref...} — ref may itself contain slashes (feature/x).
        if (segments.Length >= 4 && segments[2].Equals("tree", StringComparison.OrdinalIgnoreCase))
        {
            @ref = string.Join('/', segments[3..]);
            if (@ref.Contains("..")) return false;
        }

        result = new GitHubRepoRef(owner, repo, @ref);
        return true;
    }

    // GitHub owner/repo names: letters, digits, '-', '_', '.'; no traversal.
    private static bool IsSegment(string s) =>
        s.Length > 0 && s != "." && s != ".." &&
        s.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.');
}
