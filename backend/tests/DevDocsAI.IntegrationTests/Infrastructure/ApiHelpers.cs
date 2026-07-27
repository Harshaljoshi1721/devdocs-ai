using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace DevDocsAI.IntegrationTests.Infrastructure;

internal sealed record AuthTokenResponse(string AccessToken, DateTime ExpiresAt, UserModel User);
internal sealed record UserModel(Guid Id, string Email, string Name);
internal sealed record ProjectModel(
    Guid Id, string Name, string? Description, Guid OwnerId, DateTime CreatedAt, DateTime UpdatedAt);

internal static class ApiHelpers
{
    /// <summary>Creates a fresh client, registers a unique user, and attaches the bearer token.</summary>
    public static async Task<(HttpClient Client, Guid UserId, string Email)> RegisterAsync(
        this DevDocsApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, name = "Test User", password = "password123" });
        response.EnsureSuccessStatusCode();

        var body = (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        return (client, body.User.Id, email);
    }

    public static async Task<Guid> CreateProjectAsync(this HttpClient client, string name = "Test Project")
    {
        var response = await client.PostAsJsonAsync("/api/v1/projects", new { name, description = (string?)null });
        response.EnsureSuccessStatusCode();
        var project = (await response.Content.ReadFromJsonAsync<ProjectModel>())!;
        return project.Id;
    }
}
