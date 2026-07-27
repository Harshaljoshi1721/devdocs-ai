using DevDocsAI.Application.Features.Repositories;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class GitHubUrlParserTests
{
    [Theory]
    [InlineData("https://github.com/octo/cat", "octo", "cat", null)]
    [InlineData("https://github.com/octo/cat.git", "octo", "cat", null)]
    [InlineData("https://github.com/octo/cat/", "octo", "cat", null)]
    [InlineData("https://www.github.com/octo/cat", "octo", "cat", null)]
    [InlineData("https://github.com/octo/cat/tree/main", "octo", "cat", "main")]
    [InlineData("https://github.com/octo/cat/tree/feature/x", "octo", "cat", "feature/x")]
    public void Parses_valid_public_github_urls(string url, string owner, string repo, string? @ref)
    {
        GitHubUrlParser.TryParse(url, out var result).ShouldBeTrue();
        result!.Owner.ShouldBe(owner);
        result.Repo.ShouldBe(repo);
        result.Ref.ShouldBe(@ref);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("http://github.com/octo/cat")]              // must be https
    [InlineData("https://gitlab.com/octo/cat")]             // wrong host
    [InlineData("git@github.com:octo/cat.git")]             // ssh
    [InlineData("https://github.com/octo")]                 // missing repo
    [InlineData("https://github.com/")]                     // missing both
    [InlineData("https://user:pass@github.com/octo/cat")]   // embedded credentials
    [InlineData("https://github.com/../../etc/passwd")]     // traversal-ish
    public void Rejects_invalid_or_unsafe_urls(string url)
    {
        GitHubUrlParser.TryParse(url, out var result).ShouldBeFalse();
        result.ShouldBeNull();
    }
}
