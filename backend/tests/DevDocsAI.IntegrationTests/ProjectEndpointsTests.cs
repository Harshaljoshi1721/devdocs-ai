using System.Net;
using System.Net.Http.Json;
using DevDocsAI.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ProjectEndpointsTests(DevDocsApiFactory factory)
{
    [Fact]
    public async Task Projects_endpoint_requires_authentication()
    {
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/projects");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Full_crud_lifecycle_for_owner()
    {
        var (client, userId, _) = await factory.RegisterAsync();

        // Create
        var create = await client.PostAsJsonAsync("/api/v1/projects",
            new { name = "My Codebase", description = "notes" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await create.Content.ReadFromJsonAsync<ProjectModel>())!;
        created.OwnerId.ShouldBe(userId);

        // Get
        var get = await client.GetFromJsonAsync<ProjectModel>($"/api/v1/projects/{created.Id}");
        get!.Name.ShouldBe("My Codebase");

        // List contains it
        var list = await client.GetFromJsonAsync<List<ProjectModel>>("/api/v1/projects");
        list!.ShouldContain(p => p.Id == created.Id);

        // Update
        var update = await client.PutAsJsonAsync($"/api/v1/projects/{created.Id}",
            new { name = "Renamed", description = (string?)null });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await update.Content.ReadFromJsonAsync<ProjectModel>())!.Name.ShouldBe("Renamed");

        // Delete
        (await client.DeleteAsync($"/api/v1/projects/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v1/projects/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_user_cannot_access_another_users_project()
    {
        var (alice, _, _) = await factory.RegisterAsync();
        var (bob, _, _) = await factory.RegisterAsync();

        var created = (await (await alice.PostAsJsonAsync("/api/v1/projects",
            new { name = "Alice private", description = (string?)null }))
            .Content.ReadFromJsonAsync<ProjectModel>())!;

        // Bob is denied on every operation — reported as 404 (no existence leak).
        (await bob.GetAsync($"/api/v1/projects/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bob.PutAsJsonAsync($"/api/v1/projects/{created.Id}", new { name = "hijack", description = (string?)null }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bob.DeleteAsync($"/api/v1/projects/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // And Bob's own listing never includes it.
        var bobList = await bob.GetFromJsonAsync<List<ProjectModel>>("/api/v1/projects");
        bobList!.ShouldNotContain(p => p.Id == created.Id);

        // Alice still has full access.
        (await alice.GetAsync($"/api/v1/projects/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
