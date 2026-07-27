namespace DevDocsAI.Application.Abstractions;

/// <summary>
/// An in-process work queue for deferring work off the request thread. Each
/// item is given a fresh service scope when it runs. The concrete document
/// processing pipeline is enqueued here in Phase 4+.
/// </summary>
public interface IBackgroundTaskQueue
{
    ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem, CancellationToken ct);

    ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(CancellationToken ct);
}
