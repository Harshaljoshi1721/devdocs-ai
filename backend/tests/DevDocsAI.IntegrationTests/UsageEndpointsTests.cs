using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DevDocsAI.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class UsageEndpointsTests(DevDocsApiFactory factory)
{
    private sealed record DocumentModel(Guid Id, string Status);
    private sealed record UploadResult(List<DocumentModel> Accepted);
    private sealed record UsageByKind(string Kind, int Requests, long TokensIn, long TokensOut);
    private sealed record UsageSummary(int TotalRequests, long TotalTokensIn, long TotalTokensOut, List<UsageByKind> ByKind);

    private const string Fact = "the answer is 42";

    [Fact]
    public async Task Asking_a_question_records_usage()
    {
        var (client, projectId) = await IndexedProjectAsync();

        var before = (await client.GetFromJsonAsync<UsageSummary>(
            $"/api/v1/projects/{projectId}/usage"))!;
        before.TotalRequests.ShouldBe(0);

        await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/ask", new { question = Fact });

        var after = (await client.GetFromJsonAsync<UsageSummary>(
            $"/api/v1/projects/{projectId}/usage"))!;
        after.TotalRequests.ShouldBe(1);
        after.ByKind.ShouldContain(k => k.Kind == "Ask" && k.Requests == 1);
        after.TotalTokensOut.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Another_user_cannot_read_usage()
    {
        var (_, projectId) = await IndexedProjectAsync();
        var (intruder, _, _) = await factory.RegisterAsync();

        var response = await intruder.GetAsync($"/api/v1/projects/{projectId}/usage");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

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

    private static async Task WaitUntilProcessedAsync(HttpClient client, Guid projectId, Guid documentId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var docs = await client.GetFromJsonAsync<List<DocumentModel>>(
                $"/api/v1/projects/{projectId}/documents");
            var doc = docs!.Single(d => d.Id == documentId);
            if (doc.Status is "Completed" or "Failed") { doc.Status.ShouldBe("Completed"); return; }
            await Task.Delay(200);
        }

        throw new Xunit.Sdk.XunitException("Document was not processed in time.");
    }
}
