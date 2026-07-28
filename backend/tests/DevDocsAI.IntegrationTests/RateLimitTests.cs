using System.Net;
using System.Net.Http.Json;
using DevDocsAI.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class RateLimitTests(DevDocsApiFactory factory)
{
    [Fact]
    public async Task Auth_endpoint_returns_429_after_the_limit()
    {
        // Isolated host with a tiny auth budget (2 per window); reuses the fixture DB.
        var limited = factory.WithWebHostBuilder(b =>
            b.UseSetting("RateLimit:AuthPermitPerWindow", "2"));
        var client = limited.CreateClient();
        var body = new { email = "nobody@example.com", password = "wrong-password" };

        var r1 = await client.PostAsJsonAsync("/api/v1/auth/login", body);
        var r2 = await client.PostAsJsonAsync("/api/v1/auth/login", body);
        var r3 = await client.PostAsJsonAsync("/api/v1/auth/login", body);

        // The first two consume the budget (401 for bad creds); the third is limited.
        r3.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
