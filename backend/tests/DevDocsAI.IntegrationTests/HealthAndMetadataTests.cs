using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevDocsAI.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class HealthAndMetadataTests(DevDocsApiFactory factory)
{
    [Fact]
    public async Task Health_endpoint_reports_healthy_with_database_up()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().ShouldBe("Healthy");
        // The database readiness check is included and passing.
        body.GetProperty("checks").EnumerateArray()
            .ShouldContain(c => c.GetProperty("name").GetString() == "database");
    }

    [Fact]
    public async Task Info_endpoint_returns_app_metadata()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/info");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().ShouldBe("DevDocs AI");
        body.GetProperty("status").GetString().ShouldBe("ok");
    }
}
