using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DevDocsAI.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AgentEndpointsTests(DevDocsApiFactory factory)
{
    private sealed record DocumentModel(Guid Id, string Status);
    private sealed record UploadResult(List<DocumentModel> Accepted);
    private sealed record TraceItem(int Sequence, string ToolName, string Status);
    private sealed record RunResponse(Guid Id, string AgentType, string Status, string? Output, int Iterations, List<TraceItem> Trace);
    private sealed record RunSummary(Guid Id, string AgentType, string Status);
    private sealed record AgentInfo(string Type, string DisplayName, string Description);

    private const string Fact = "user registration is handled in AuthController.Register";

    [Fact]
    public async Task Lists_the_four_built_in_agents()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var agents = await client.GetFromJsonAsync<List<AgentInfo>>(
            $"/api/v1/projects/{projectId}/agents");

        agents!.Count.ShouldBe(4);
        agents.ShouldContain(a => a.Type == "CodeExplorer");
        agents.ShouldContain(a => a.Type == "BugAnalysis");
    }

    [Fact]
    public async Task Run_uses_a_tool_answers_and_persists_the_trace()
    {
        var (client, projectId) = await IndexedProjectAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/agents/CodeExplorer/run",
            new { input = "where is user registration?" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var run = (await response.Content.ReadFromJsonAsync<RunResponse>())!;
        run.Status.ShouldBe("Completed");
        run.Output.ShouldNotBeNullOrWhiteSpace();
        run.Trace.ShouldContain(t => t.ToolName == "SearchProject" && t.Status == "Ok");

        // Re-viewable via history.
        var fetched = await client.GetFromJsonAsync<RunResponse>(
            $"/api/v1/projects/{projectId}/agents/runs/{run.Id}");
        fetched!.Trace.Count.ShouldBe(run.Trace.Count);

        var runs = await client.GetFromJsonAsync<List<RunSummary>>(
            $"/api/v1/projects/{projectId}/agents/runs");
        runs!.ShouldContain(r => r.Id == run.Id);
    }

    [Fact]
    public async Task Unknown_agent_type_is_not_found()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/agents/Wizard/run", new { input = "hi" });
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Empty_input_is_rejected()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/agents/CodeExplorer/run", new { input = "   " });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Another_user_cannot_run_or_read_runs()
    {
        var (owner, projectId) = await IndexedProjectAsync();
        var run = (await (await owner.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/agents/CodeExplorer/run", new { input = "q" }))
            .Content.ReadFromJsonAsync<RunResponse>())!;

        var (intruder, _, _) = await factory.RegisterAsync();

        (await intruder.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/agents/CodeExplorer/run", new { input = "q" }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.GetAsync(
            $"/api/v1/projects/{projectId}/agents/runs/{run.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<(HttpClient client, Guid projectId)> IndexedProjectAsync()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(Fact));
        part.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(part, "files", "notes.md");

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
