using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Application.Features.Agents;
using DevDocsAI.Application.Features.Agents.Tools;
using DevDocsAI.Application.Features.Auth;
using DevDocsAI.Application.Features.Chat;
using DevDocsAI.Application.Features.Ingestion;
using DevDocsAI.Application.Features.Processing;
using DevDocsAI.Application.Features.Projects;
using DevDocsAI.Application.Features.Rag;
using DevDocsAI.Application.Features.Repositories;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DevDocsAI.Application;

public static class DependencyInjection
{
    /// <summary>Registers Application use-case services and validators.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<ApplicationAssemblyReference>(ServiceLifetime.Scoped);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IDocumentIngestor, DocumentIngestor>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDocumentProcessor, DocumentProcessor>();
        services.AddScoped<IRetrievalService, RetrievalService>();
        services.AddScoped<IRagService, RagService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IRepositoryConnectionService, RepositoryConnectionService>();
        services.AddScoped<IRepositoryIngestor, RepositoryIngestor>();
        services.AddScoped<IAgentTool, SearchProjectTool>();
        services.AddScoped<IAgentTool, ReadFileTool>();
        services.AddScoped<IAgentTool, GetProjectStructureTool>();
        services.AddScoped<ToolRegistry>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddSingleton<IFileFilter, ExtensionFileFilter>();
        services.AddSingleton<ITextChunker, LineAwareChunker>();
        services.AddSingleton<IReranker, PassthroughReranker>();

        return services;
    }
}
