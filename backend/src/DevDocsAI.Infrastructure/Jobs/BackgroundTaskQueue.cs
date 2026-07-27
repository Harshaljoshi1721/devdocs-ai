using System.Threading.Channels;
using DevDocsAI.Application.Abstractions;

namespace DevDocsAI.Infrastructure.Jobs;

/// <summary>In-process, unbounded work queue backed by a channel.</summary>
public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, ValueTask>> _channel =
        Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, ValueTask>>();

    public ValueTask EnqueueAsync(
        Func<IServiceProvider, CancellationToken, ValueTask> workItem, CancellationToken ct) =>
        _channel.Writer.WriteAsync(workItem, ct);

    public ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}
