using DevDocsAI.Application.Abstractions;

namespace DevDocsAI.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
