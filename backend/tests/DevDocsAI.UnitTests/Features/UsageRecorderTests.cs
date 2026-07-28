using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Features.Usage;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class UsageRecorderTests
{
    private readonly IUsageRecordRepository _repo = Substitute.For<IUsageRecordRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly UsageRecorder _sut;
    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly Guid _projectId = Guid.CreateVersion7();

    public UsageRecorderTests() =>
        _sut = new UsageRecorder(_repo, _uow, NullLogger<UsageRecorder>.Instance);

    [Fact]
    public async Task Record_persists_a_usage_record()
    {
        await _sut.RecordAsync(_userId, _projectId, UsageKind.Chat, 100, 20, default);

        await _repo.Received(1).AddAsync(
            Arg.Is<UsageRecord>(r => r != null && r.Kind == UsageKind.Chat && r.TokensIn == 100 && r.TokensOut == 20),
            default);
        await _uow.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Record_swallows_failures_so_it_never_breaks_the_caller()
    {
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Throws(new InvalidOperationException("db down"));

        await Should.NotThrowAsync(() => _sut.RecordAsync(_userId, _projectId, UsageKind.Ask, 1, 1, default));
    }
}
