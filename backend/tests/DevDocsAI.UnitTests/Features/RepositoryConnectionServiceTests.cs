using DevDocsAI.Application.Abstractions;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Ingestion;
using DevDocsAI.Application.Features.Repositories;
using DevDocsAI.Domain.Entities;
using DevDocsAI.Domain.Enums;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class RepositoryConnectionServiceTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IRepositoryConnectionRepository _connections = Substitute.For<IRepositoryConnectionRepository>();
    private readonly IDocumentService _documents = Substitute.For<IDocumentService>();
    private readonly IBackgroundTaskQueue _queue = Substitute.For<IBackgroundTaskQueue>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly RepositoryConnectionService _sut;

    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly Guid _projectId = Guid.CreateVersion7();

    public RepositoryConnectionServiceTests()
    {
        _sut = new RepositoryConnectionService(_projects, _connections, _documents, _queue, _uow);
        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, _userId));
    }

    [Fact]
    public async Task Connect_creates_a_pending_connection_and_enqueues_ingestion()
    {
        var response = await _sut.ConnectAsync(
            _userId, _projectId, new ConnectRepositoryRequest("https://github.com/octo/cat", null), default);

        response.Owner.ShouldBe("octo");
        response.Repo.ShouldBe("cat");
        response.Status.ShouldBe(nameof(ProcessingStatus.Pending));
        await _connections.Received(1).AddAsync(Arg.Any<RepositoryConnection>(), Arg.Any<CancellationToken>());
        await _uow.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _queue.Received(1).EnqueueAsync(
            Arg.Any<Func<IServiceProvider, CancellationToken, ValueTask>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Connect_replaces_an_existing_connection()
    {
        var existing = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/old/repo", "old", "repo", null);
        _connections.GetByProjectAsync(_projectId, Arg.Any<CancellationToken>()).Returns(existing);

        await _sut.ConnectAsync(
            _userId, _projectId, new ConnectRepositoryRequest("https://github.com/octo/cat", null), default);

        await _documents.Received(1).RemoveByConnectionAsync(existing.Id, Arg.Any<CancellationToken>());
        _connections.Received(1).Remove(existing);
    }

    [Fact]
    public async Task Connect_with_an_invalid_url_is_a_validation_error()
    {
        await Should.ThrowAsync<ValidationException>(() => _sut.ConnectAsync(
            _userId, _projectId, new ConnectRepositoryRequest("not-a-repo", null), default));
    }

    [Fact]
    public async Task Connect_on_another_users_project_is_not_found()
    {
        _projects.GetByIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(Project.Create("Proj", null, Guid.CreateVersion7()));

        await Should.ThrowAsync<NotFoundException>(() => _sut.ConnectAsync(
            _userId, _projectId, new ConnectRepositoryRequest("https://github.com/octo/cat", null), default));
    }

    [Fact]
    public async Task Get_returns_null_when_no_connection_exists()
    {
        var result = await _sut.GetAsync(_userId, _projectId, default);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Disconnect_removes_docs_and_connection()
    {
        var existing = RepositoryConnection.Connect(
            _projectId, RepositoryProvider.GitHub, "https://github.com/octo/cat", "octo", "cat", null);
        _connections.GetByProjectAsync(_projectId, Arg.Any<CancellationToken>()).Returns(existing);

        await _sut.DisconnectAsync(_userId, _projectId, default);

        await _documents.Received(1).RemoveByConnectionAsync(existing.Id, Arg.Any<CancellationToken>());
        _connections.Received(1).Remove(existing);
        await _uow.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
