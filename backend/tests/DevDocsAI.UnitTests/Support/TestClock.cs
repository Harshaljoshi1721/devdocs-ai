using DevDocsAI.Application.Abstractions;

namespace DevDocsAI.UnitTests.Support;

public sealed class TestClock(DateTime? now = null) : IClock
{
    public DateTime UtcNow { get; set; } = now ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
