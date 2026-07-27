using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace DevDocsAI.IntegrationTests.Infrastructure;

/// <summary>
/// Spins up a real PostgreSQL (with pgvector) via Testcontainers, points the
/// API at it, and applies migrations — so integration tests exercise the full
/// HTTP + EF Core + database stack.
/// </summary>
public sealed class DevDocsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .Build();

    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), $"devdocs-tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting reliably reaches builder.Configuration under the minimal
        // hosting model (ConfigureAppConfiguration does not always apply in time).
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789abcd");
        builder.UseSetting("Jwt:Issuer", "devdocs-ai");
        builder.UseSetting("Jwt:Audience", "devdocs-ai");
        builder.UseSetting("Storage:RootPath", _storageRoot);

        // Swap the real Gemini providers for deterministic fakes — no network, no API key.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmbeddingService>();
            services.RemoveAll<IChatCompletionService>();
            services.AddSingleton<IEmbeddingService, FakeEmbeddingService>();
            services.AddSingleton<IChatCompletionService, FakeChatCompletionService>();
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<DevDocsApiFactory>
{
    public const string Name = "api";
}
