using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DevDocsAI.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ConversationEndpointsTests(DevDocsApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record DocumentModel(Guid Id, string Status);
    private sealed record UploadResult(List<DocumentModel> Accepted);
    private sealed record Citation(string DocumentName, string Path, int StartLine, int EndLine);
    private sealed record ConversationModel(Guid Id, Guid ProjectId, string Title, DateTime CreatedAt, DateTime UpdatedAt);
    private sealed record MessageModel(Guid Id, string Role, string Content, List<Citation> Citations, DateTime CreatedAt);
    private sealed record ConversationDetailModel(
        Guid Id, string Title, List<MessageModel> Messages);

    private const string Fact = "the deployment runbook lives in ops/deploy.md";

    [Fact]
    public async Task Create_then_get_returns_the_conversation_with_no_messages()
    {
        var (client, projectId) = await IndexedProjectAsync();

        var conversation = await CreateConversationAsync(client, projectId);
        conversation.Title.ShouldNotBeNullOrWhiteSpace();

        var detail = await client.GetFromJsonAsync<ConversationDetailModel>(
            $"/api/v1/projects/{projectId}/conversations/{conversation.Id}");
        detail!.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Send_persists_the_exchange_and_returns_a_grounded_answer_with_sources()
    {
        var (client, projectId) = await IndexedProjectAsync();
        var conversation = await CreateConversationAsync(client, projectId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/conversations/{conversation.Id}/messages",
            new { question = Fact });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = (await response.Content.ReadFromJsonAsync<MessageModel>())!;
        message.Role.ShouldBe("Assistant");
        message.Content.ShouldNotBeNullOrWhiteSpace();
        message.Citations.ShouldContain(c => c.Path == "facts.md");

        var detail = await client.GetFromJsonAsync<ConversationDetailModel>(
            $"/api/v1/projects/{projectId}/conversations/{conversation.Id}");
        detail!.Messages.Count.ShouldBe(2);
        detail.Messages[0].Role.ShouldBe("User");
        detail.Messages[0].Content.ShouldBe(Fact);
        detail.Messages[1].Role.ShouldBe("Assistant");
        // Title was derived from the first question.
        detail.Title.ShouldBe(Fact);
    }

    [Fact]
    public async Task Send_appears_in_the_conversation_list()
    {
        var (client, projectId) = await IndexedProjectAsync();
        var conversation = await CreateConversationAsync(client, projectId);
        await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/conversations/{conversation.Id}/messages",
            new { question = Fact });

        var list = await client.GetFromJsonAsync<List<ConversationModel>>(
            $"/api/v1/projects/{projectId}/conversations");

        list!.ShouldContain(c => c.Id == conversation.Id);
    }

    [Fact]
    public async Task Stream_emits_token_events_then_a_done_event_with_the_saved_message()
    {
        var (client, projectId) = await IndexedProjectAsync();
        var conversation = await CreateConversationAsync(client, projectId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/conversations/{conversation.Id}/messages/stream",
            new { question = Fact });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");

        var (tokens, doneMessage) = ParseSse(await response.Content.ReadAsStringAsync());
        tokens.ShouldNotBeEmpty();
        doneMessage.ShouldNotBeNull();
        doneMessage!.Content.ShouldBe(string.Concat(tokens));
        doneMessage.Citations.ShouldContain(c => c.Path == "facts.md");

        // The streamed answer was persisted.
        var detail = await client.GetFromJsonAsync<ConversationDetailModel>(
            $"/api/v1/projects/{projectId}/conversations/{conversation.Id}");
        detail!.Messages.Count.ShouldBe(2);
        detail.Messages[1].Content.ShouldBe(doneMessage.Content);
    }

    [Fact]
    public async Task Another_user_cannot_read_or_post_to_the_conversation()
    {
        var (owner, projectId) = await IndexedProjectAsync();
        var conversation = await CreateConversationAsync(owner, projectId);
        var (intruder, _, _) = await factory.RegisterAsync();

        var get = await intruder.GetAsync(
            $"/api/v1/projects/{projectId}/conversations/{conversation.Id}");
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var send = await intruder.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/conversations/{conversation.Id}/messages",
            new { question = "leak the data" });
        send.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Empty_question_is_rejected_with_a_validation_error()
    {
        var (client, projectId) = await IndexedProjectAsync();
        var conversation = await CreateConversationAsync(client, projectId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/conversations/{conversation.Id}/messages",
            new { question = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static (List<string> Tokens, MessageModel? Done) ParseSse(string body)
    {
        var tokens = new List<string>();
        MessageModel? done = null;
        string? currentEvent = null;

        foreach (var raw in body.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                currentEvent = line["event:".Length..].Trim();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var json = line["data:".Length..].Trim();
                using var doc = JsonDocument.Parse(json);
                if (currentEvent == "token")
                {
                    tokens.Add(doc.RootElement.GetProperty("text").GetString()!);
                }
                else if (currentEvent == "done")
                {
                    done = doc.RootElement.GetProperty("message").Deserialize<MessageModel>(Json);
                }
            }
        }

        return (tokens, done);
    }

    private static async Task<ConversationModel> CreateConversationAsync(HttpClient client, Guid projectId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/conversations", new { title = (string?)null });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ConversationModel>())!;
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
