using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DevDocsAI.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class RagEndpointsTests(DevDocsApiFactory factory)
{
    private sealed record DocumentModel(Guid Id, string Status);
    private sealed record UploadResult(List<DocumentModel> Accepted);
    private sealed record SearchHit(string DocumentName, string Path, int StartLine, int EndLine, double Score, string Snippet);
    private sealed record SearchResponse(string Query, List<SearchHit> Results);
    private sealed record Citation(string DocumentName, string Path, int StartLine, int EndLine);
    private sealed record AskResponse(string Answer, List<Citation> Sources, bool Grounded);

    private const string Fact = "the secret answer is forty two";

    private async Task<(HttpClient client, Guid projectId)> IndexedProjectAsync()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(Fact));
        part.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(part, "files", "facts.md");

        var upload = (await (await client.PostAsync($"/api/v1/projects/{projectId}/documents", form))
            .Content.ReadFromJsonAsync<UploadResult>())!;
        await WaitUntilProcessedAsync(client, projectId, upload.Accepted.Single().Id);

        return (client, projectId);
    }

    [Fact]
    public async Task Search_returns_the_relevant_chunk_with_score_and_location()
    {
        var (client, projectId) = await IndexedProjectAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/search", new { query = Fact });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<SearchResponse>())!;
        body.Results.ShouldNotBeEmpty();
        body.Results[0].Path.ShouldBe("facts.md");
        body.Results[0].Snippet.ShouldContain("forty two");
    }

    [Fact]
    public async Task Ask_returns_a_grounded_answer_with_sources()
    {
        var (client, projectId) = await IndexedProjectAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/ask", new { question = Fact });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<AskResponse>())!;
        body.Grounded.ShouldBeTrue();
        body.Answer.ShouldNotBeNullOrWhiteSpace();
        body.Sources.ShouldContain(s => s.Path == "facts.md");
    }

    [Fact]
    public async Task Ask_on_a_project_with_no_indexed_content_is_not_grounded()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/ask", new { question = "anything at all" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<AskResponse>())!;
        body.Grounded.ShouldBeFalse();
        body.Sources.ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_on_another_users_project_is_denied()
    {
        var (_, projectId) = await IndexedProjectAsync();
        var (bob, _, _) = await factory.RegisterAsync();

        var response = await bob.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/search", new { query = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task WaitUntilProcessedAsync(HttpClient client, Guid projectId, Guid documentId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var docs = await client.GetFromJsonAsync<List<DocumentModel>>(
                $"/api/v1/projects/{projectId}/documents");
            var doc = docs!.Single(d => d.Id == documentId);
            if (doc.Status is "Completed" or "Failed")
            {
                doc.Status.ShouldBe("Completed");
                return;
            }

            await Task.Delay(200);
        }

        throw new Xunit.Sdk.XunitException("Document was not processed in time.");
    }
}
