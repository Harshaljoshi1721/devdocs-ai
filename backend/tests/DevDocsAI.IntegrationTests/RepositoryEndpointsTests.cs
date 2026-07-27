using System.Net;
using System.Net.Http.Json;
using DevDocsAI.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class RepositoryEndpointsTests(DevDocsApiFactory factory)
{
    private sealed record ConnectionModel(
        Guid Id, string Owner, string Repo, string? Ref, string? CommitSha, string Status, int FileCount);
    private sealed record DocumentModel(Guid Id, string Path, string Status);
    private sealed record SearchHit(string Path);
    private sealed record SearchResponse(string Query, List<SearchHit> Results);

    [Fact]
    public async Task Connect_ingests_supported_files_and_reports_completed()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var connect = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/repository", new { url = "https://github.com/octo/cat" });
        connect.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var connection = await WaitForStatusAsync(client, projectId, "Completed");
        connection.CommitSha.ShouldBe(FakeGitHubRepositoryClient.CommitSha);
        connection.FileCount.ShouldBe(2); // auth.cs + architecture.md; .env and .png skipped

        var docs = (await client.GetFromJsonAsync<List<DocumentModel>>(
            $"/api/v1/projects/{projectId}/documents"))!;
        var paths = docs.Select(d => d.Path).ToList();
        paths.ShouldContain("src/auth.cs");
        paths.ShouldContain("docs/architecture.md");
        paths.ShouldNotContain(".env");
    }

    [Fact]
    public async Task Ingested_repo_content_is_searchable()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();
        await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/repository", new { url = "https://github.com/octo/cat" });
        await WaitForStatusAsync(client, projectId, "Completed");
        await WaitUntilDocsProcessedAsync(client, projectId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/search", new { query = "how does authentication work" });
        var body = (await response.Content.ReadFromJsonAsync<SearchResponse>())!;
        body.Results.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Disconnect_removes_the_connection_and_its_documents()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();
        await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/repository", new { url = "https://github.com/octo/cat" });
        await WaitForStatusAsync(client, projectId, "Completed");

        var delete = await client.DeleteAsync($"/api/v1/projects/{projectId}/repository");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"/api/v1/projects/{projectId}/repository");
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var docs = await client.GetFromJsonAsync<List<DocumentModel>>(
            $"/api/v1/projects/{projectId}/documents");
        docs!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Invalid_url_is_rejected()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/repository", new { url = "https://gitlab.com/octo/cat" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Another_user_cannot_read_the_connection()
    {
        var (owner, _, _) = await factory.RegisterAsync();
        var projectId = await owner.CreateProjectAsync();
        await owner.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/repository", new { url = "https://github.com/octo/cat" });

        var (intruder, _, _) = await factory.RegisterAsync();
        var get = await intruder.GetAsync($"/api/v1/projects/{projectId}/repository");
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<ConnectionModel> WaitForStatusAsync(HttpClient client, Guid projectId, string target)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var response = await client.GetAsync($"/api/v1/projects/{projectId}/repository");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var model = (await response.Content.ReadFromJsonAsync<ConnectionModel>())!;
                if (model.Status is "Completed" or "Failed")
                {
                    model.Status.ShouldBe(target);
                    return model;
                }
            }

            await Task.Delay(200);
        }

        throw new Xunit.Sdk.XunitException("Repository connection did not reach a terminal status in time.");
    }

    private static async Task WaitUntilDocsProcessedAsync(HttpClient client, Guid projectId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var docs = await client.GetFromJsonAsync<List<DocumentModel>>(
                $"/api/v1/projects/{projectId}/documents");
            if (docs!.Count > 0 && docs.All(d => d.Status is "Completed" or "Failed"))
            {
                docs.ShouldAllBe(d => d.Status == "Completed");
                return;
            }

            await Task.Delay(200);
        }

        throw new Xunit.Sdk.XunitException("Repository documents were not processed in time.");
    }
}
