using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DevDocsAI.IntegrationTests.Infrastructure;
using DevDocsAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DocumentProcessingTests(DevDocsApiFactory factory)
{
    private sealed record DocumentModel(Guid Id, string Name, string Status);
    private sealed record UploadResult(List<DocumentModel> Accepted);

    [Fact]
    public async Task Uploaded_document_is_processed_into_chunks_in_the_background()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var content = string.Join('\n', Enumerable.Range(1, 40).Select(i => $"line number {i}"));
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        part.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(part, "files", "sample.cs");

        var upload = (await (await client.PostAsync($"/api/v1/projects/{projectId}/documents", form))
            .Content.ReadFromJsonAsync<UploadResult>())!;
        var documentId = upload.Accepted.Single().Id;

        // The background pipeline moves the document Pending -> Processing -> Completed.
        var status = await PollUntilTerminalAsync(client, projectId, documentId);
        status.ShouldBe("Completed");

        // Chunks were persisted, with sane, 1-based line ranges.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var chunks = await db.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync();

        chunks.ShouldNotBeEmpty();
        chunks[0].StartLine.ShouldBe(1);
        chunks.ShouldAllBe(c => c.EndLine >= c.StartLine);
        chunks.ShouldAllBe(c => c.Content.Length > 0);
    }

    private static async Task<string> PollUntilTerminalAsync(HttpClient client, Guid projectId, Guid documentId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var docs = await client.GetFromJsonAsync<List<DocumentModel>>(
                $"/api/v1/projects/{projectId}/documents");
            var doc = docs!.Single(d => d.Id == documentId);
            if (doc.Status is "Completed" or "Failed")
            {
                return doc.Status;
            }

            await Task.Delay(200);
        }

        return "timeout";
    }
}
