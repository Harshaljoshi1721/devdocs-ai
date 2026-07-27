using System.Net;
using System.Net.Http.Json;
using DevDocsAI.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthEndpointsTests(DevDocsApiFactory factory)
{
    [Fact]
    public async Task Register_returns_access_token_and_sets_httponly_refresh_cookie()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, name = "Alice", password = "password123" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.User.Email.ShouldBe(email);

        var setCookie = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        setCookie.ShouldContain(c => c.StartsWith("refresh_token=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Duplicate_email_is_conflict()
    {
        var client = factory.CreateClient();
        var email = $"dupe-{Guid.NewGuid():N}@example.com";
        var payload = new { email, name = "Bob", password = "password123" };

        (await client.PostAsJsonAsync("/api/v1/auth/register", payload)).EnsureSuccessStatusCode();
        var second = await client.PostAsJsonAsync("/api/v1/auth/register", payload);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_succeeds_with_correct_password_and_fails_otherwise()
    {
        var client = factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, name = "Carol", password = "password123" })).EnsureSuccessStatusCode();

        var ok = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "password123" });
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);

        var bad = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "wrong-password" });
        bad.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_with_invalid_input_returns_validation_problem()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = "not-an-email", name = "", password = "short" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Refresh_cookie_issues_a_new_access_token()
    {
        // The default WebApplicationFactory client retains Set-Cookie, so the
        // refresh cookie from register is sent back on the refresh call.
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = $"refresh-{Guid.NewGuid():N}@example.com", name = "Dave", password = "password123" }))
            .EnsureSuccessStatusCode();

        var refresh = await client.PostAsync("/api/v1/auth/refresh", null);

        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await refresh.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }
}
