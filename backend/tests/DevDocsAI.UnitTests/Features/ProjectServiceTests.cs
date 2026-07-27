using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Projects;
using DevDocsAI.Domain.Entities;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class ProjectServiceTests
{
    private readonly IProjectRepository _repo = Substitute.For<IProjectRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ProjectService _sut;

    public ProjectServiceTests()
    {
        _sut = new ProjectService(
            _repo, _uow,
            new CreateProjectRequestValidator(),
            new UpdateProjectRequestValidator());
    }

    [Fact]
    public async Task Create_persists_and_returns_project_owned_by_caller()
    {
        var userId = Guid.CreateVersion7();

        var result = await _sut.CreateAsync(userId, new CreateProjectRequest("My Project", "desc"), default);

        result.Name.ShouldBe("My Project");
        result.OwnerId.ShouldBe(userId);
        await _repo.Received(1).AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_anothers_project_is_reported_as_not_found()
    {
        var owner = Guid.CreateVersion7();
        var otherUser = Guid.CreateVersion7();
        var project = Project.Create("Owner's project", null, owner);
        _repo.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        await Should.ThrowAsync<NotFoundException>(
            () => _sut.GetAsync(otherUser, project.Id, default));
    }

    [Fact]
    public async Task Update_anothers_project_is_denied()
    {
        var owner = Guid.CreateVersion7();
        var project = Project.Create("Owner's project", null, owner);
        _repo.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        await Should.ThrowAsync<NotFoundException>(
            () => _sut.UpdateAsync(Guid.CreateVersion7(), project.Id, new UpdateProjectRequest("x", null), default));
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_missing_project_is_not_found()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Project?)null);

        await Should.ThrowAsync<NotFoundException>(
            () => _sut.GetAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), default));
    }
}
