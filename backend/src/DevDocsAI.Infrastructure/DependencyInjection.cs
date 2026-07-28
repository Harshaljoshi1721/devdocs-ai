using DevDocsAI.Application.Abstractions;
using DevDocsAI.Application.Features.Agents;
using Microsoft.Extensions.Options;
using DevDocsAI.Infrastructure.GitHub;
using DevDocsAI.Application.Features.Repositories;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Application.Abstractions.Storage;
using DevDocsAI.Application.Features.Ingestion;
using DevDocsAI.Application.Features.Processing;
using DevDocsAI.Infrastructure.AI;
using DevDocsAI.Infrastructure.Jobs;
using DevDocsAI.Infrastructure.Persistence;
using DevDocsAI.Infrastructure.Persistence.Repositories;
using DevDocsAI.Infrastructure.Security;
using DevDocsAI.Infrastructure.Storage;
using DevDocsAI.Infrastructure.Time;
using DevDocsAI.Infrastructure.Vectors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevDocsAI.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence (EF Core + Npgsql), repositories, and security
    /// services, binding and validating configuration up front.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<UploadOptions>()
            .Bind(configuration.GetSection(UploadOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<ChunkingOptions>()
            .Bind(configuration.GetSection(ChunkingOptions.SectionName))
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

        // Embeddings always use Gemini (free tier). Options aren't validated at
        // startup so the app runs without a key; calls fail clearly when needed.
        services.AddOptions<GeminiOptions>().Bind(configuration.GetSection(GeminiOptions.SectionName));
        services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>();

        // Chat provider is configurable (Ai:ChatProvider = "Gemini" | "Ollama").
        var chatProvider = configuration["Ai:ChatProvider"] ?? "Gemini";
        if (string.Equals(chatProvider, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            services.AddOptions<OllamaOptions>().Bind(configuration.GetSection(OllamaOptions.SectionName));
            services.AddHttpClient<IChatCompletionService, OllamaChatCompletionService>()
                .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(5)); // local inference can be slow
        }
        else
        {
            services.AddHttpClient<IChatCompletionService, GeminiChatCompletionService>();
        }

        services.AddScoped<IVectorStore, PgVectorStore>();

        // GitHub repository ingestion (public repos, no auth).
        services.AddOptions<GitHubOptions>().Bind(configuration.GetSection(GitHubOptions.SectionName));
        services.AddOptions<RepoIngestionOptions>().Bind(configuration.GetSection(RepoIngestionOptions.SectionName));
        services.AddOptions<AgentOptions>().Bind(configuration.GetSection(AgentOptions.SectionName));
        services.AddHttpClient<IGitHubRepositoryClient, GitHubRepositoryClient>()
            .ConfigureHttpClient((sp, c) =>
                c.Timeout = TimeSpan.FromSeconds(
                    sp.GetRequiredService<IOptions<GitHubOptions>>().Value.TimeoutSeconds));

        // Time + security.
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();

        // File storage + background processing.
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        services.AddHostedService<QueuedHostedService>();

        // Persistence ports.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IRepositoryConnectionRepository, RepositoryConnectionRepository>();
        services.AddScoped<IAgentRunRepository, AgentRunRepository>();

        return services;
    }
}
