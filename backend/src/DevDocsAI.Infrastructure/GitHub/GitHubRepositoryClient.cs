using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DevDocsAI.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace DevDocsAI.Infrastructure.GitHub;

/// <summary>Downloads a public GitHub repository as a tar.gz via codeload, resolving the commit first.</summary>
public sealed class GitHubRepositoryClient(HttpClient http, IOptions<GitHubOptions> options)
    : IGitHubRepositoryClient
{
    private readonly GitHubOptions _options = options.Value;

    public async Task<RepositoryArchive> DownloadTarballAsync(
        string owner, string repo, string? @ref, CancellationToken ct)
    {
        // Resolve the ref to a concrete commit (also validates existence / public access).
        var commit = await ResolveCommitAsync(owner, repo, @ref, ct);

        var url = $"{_options.CodeloadBaseUrl.TrimEnd('/')}/{owner}/{repo}/tar.gz/{commit}";
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Could not reach GitHub to download {owner}/{repo}.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new InvalidOperationException(
                $"Failed to download {owner}/{repo} ({(int)response.StatusCode}).");
        }

        var stream = await response.Content.ReadAsStreamAsync(ct);
        return new RepositoryArchive(commit, stream);
    }

    private async Task<string> ResolveCommitAsync(string owner, string repo, string? @ref, CancellationToken ct)
    {
        // GET /repos/{owner}/{repo}/commits/{ref|HEAD} → the resolved commit sha.
        var reference = string.IsNullOrWhiteSpace(@ref) ? "HEAD" : @ref;
        var url = $"{_options.ApiBaseUrl.TrimEnd('/')}/repos/{owner}/{repo}/commits/{reference}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("DevDocsAI");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Could not reach GitHub for {owner}/{repo}.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidOperationException(
                    $"Repository {owner}/{repo} was not found. It must be public and the branch must exist.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new InvalidOperationException("GitHub rate limit reached. Try again later.");
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"GitHub returned {(int)response.StatusCode} for {owner}/{repo}.");

            var body = await response.Content.ReadFromJsonAsync<CommitResponse>(ct);
            if (string.IsNullOrEmpty(body?.Sha))
                throw new InvalidOperationException($"GitHub did not return a commit for {owner}/{repo}.");
            return body.Sha;
        }
    }

    private sealed record CommitResponse([property: JsonPropertyName("sha")] string Sha);
}
