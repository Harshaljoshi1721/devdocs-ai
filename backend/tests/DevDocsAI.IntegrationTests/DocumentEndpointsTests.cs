using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DevDocsAI.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DocumentEndpointsTests(DevDocsApiFactory factory)
{
    private sealed record RejectedFile(string FileName, string Reason);
    private sealed record UploadResult(List<DocumentModel> Accepted, List<RejectedFile> Rejected);
    private sealed record DocumentModel(
        Guid Id, string Name, string Path, string FileType, long Size,
        string ContentHash, string Status, string? Error);

    private static MultipartFormDataContent Multipart(params (string name, string content)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, content) in files)
        {
            var part = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            part.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(part, "files", name);
        }
        return form;
    }

    [Fact]
    public async Task Upload_supported_files_creates_pending_documents()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var response = await client.PostAsync(
            $"/api/v1/projects/{projectId}/documents",
            Multipart(("Program.cs", "class C {}"), ("README.md", "# Title")));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<UploadResult>())!;
        result.Accepted.Count.ShouldBe(2);
        result.Rejected.ShouldBeEmpty();
        result.Accepted.ShouldAllBe(d => d.Status == "Pending");
        result.Accepted.ShouldContain(d => d.Name == "Program.cs" && d.FileType == "Code");
        result.Accepted.ShouldContain(d => d.Name == "README.md" && d.FileType == "Documentation");

        var list = await client.GetFromJsonAsync<List<DocumentModel>>($"/api/v1/projects/{projectId}/documents");
        list!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Secret_and_unsupported_files_are_rejected()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var response = await client.PostAsync(
            $"/api/v1/projects/{projectId}/documents",
            Multipart((".env", "SECRET=abc123"), ("app.exe", "MZ..."), ("main.cs", "class M {}")));

        var result = (await response.Content.ReadFromJsonAsync<UploadResult>())!;
        result.Accepted.Count.ShouldBe(1);
        result.Accepted[0].Name.ShouldBe("main.cs");
        result.Rejected.ShouldContain(r => r.FileName == ".env" && r.Reason == "secret");
        result.Rejected.ShouldContain(r => r.FileName == "app.exe" && r.Reason == "unsupported");
    }

    [Fact]
    public async Task Duplicate_content_is_skipped()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var response = await client.PostAsync(
            $"/api/v1/projects/{projectId}/documents",
            Multipart(("a.cs", "identical content"), ("b.cs", "identical content")));

        var result = (await response.Content.ReadFromJsonAsync<UploadResult>())!;
        result.Accepted.Count.ShouldBe(1);
        result.Rejected.ShouldContain(r => r.Reason == "duplicate");
    }

    [Fact]
    public async Task Upload_to_another_users_project_is_denied()
    {
        var (alice, _, _) = await factory.RegisterAsync();
        var (bob, _, _) = await factory.RegisterAsync();
        var aliceProject = await alice.CreateProjectAsync("Alice private");

        var upload = await bob.PostAsync(
            $"/api/v1/projects/{aliceProject}/documents",
            Multipart(("sneaky.cs", "x")));
        upload.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var list = await bob.GetAsync($"/api/v1/projects/{aliceProject}/documents");
        list.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Documents_require_authentication()
    {
        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/v1/projects/{Guid.NewGuid()}/documents");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_removes_a_document()
    {
        var (client, _, _) = await factory.RegisterAsync();
        var projectId = await client.CreateProjectAsync();

        var result = (await (await client.PostAsync(
            $"/api/v1/projects/{projectId}/documents",
            Multipart(("solo.cs", "class S {}"))))
            .Content.ReadFromJsonAsync<UploadResult>())!;
        var documentId = result.Accepted[0].Id;

        var delete = await client.DeleteAsync($"/api/v1/projects/{projectId}/documents/{documentId}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<List<DocumentModel>>($"/api/v1/projects/{projectId}/documents");
        list!.ShouldBeEmpty();
    }
}
