using DevDocsAI.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevDocsAI.Infrastructure.Jobs;

/// <summary>
/// Drains the background task queue, running each work item in its own DI scope.
/// A failing item is logged and never brings down the worker. This hosts the
/// document-processing pipeline that Phase 4+ enqueues.
/// </summary>
public sealed class QueuedHostedService(
    IBackgroundTaskQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<QueuedHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background task queue worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            Func<IServiceProvider, CancellationToken, ValueTask> workItem;
            try
            {
                workItem = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "A background work item failed.");
            }
        }

        logger.LogInformation("Background task queue worker stopping.");
    }
}
