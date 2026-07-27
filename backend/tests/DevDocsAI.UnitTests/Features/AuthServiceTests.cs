using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Features.Auth;
using DevDocsAI.Domain.Entities;
using DevDocsAI.UnitTests.Support;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class AuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _tokens.CreateAccessToken(Arg.Any<User>())
            .Returns(new AccessToken("access", DateTime.UtcNow.AddMinutes(15)));
        _tokens.CreateRefreshToken()
            .Returns(new GeneratedRefreshToken("raw", "hash", DateTime.UtcNow.AddDays(14)));

        _sut = new AuthService(
            _users, _refreshTokens, _uow, _hasher, _tokens, new TestClock(),
            new RegisterRequestValidator(), new LoginRequestValidator());
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        _users.EmailExistsAsync("taken@example.com", Arg.Any<CancellationToken>()).Returns(true);

        await Should.ThrowAsync<ConflictException>(
            () => _sut.RegisterAsync(new RegisterRequest("Taken@Example.com", "User", "password123"), default));
    }

    [Fact]
    public async Task Register_normalizes_email_and_issues_tokens()
    {
        _users.EmailExistsAsync("new@example.com", Arg.Any<CancellationToken>()).Returns(false);
        _hasher.Hash("password123").Returns("hashed");

        var result = await _sut.RegisterAsync(new RegisterRequest("  New@Example.com ", " User ", "password123"), default);

        result.User.Email.ShouldBe("new@example.com");
        result.AccessToken.ShouldBe("access");
        result.RefreshToken.ShouldBe("raw");
        await _users.Received(1).AddAsync(Arg.Is<User>(u => u!.Email == "new@example.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_with_wrong_password_fails_with_authentication_error()
    {
        var user = new User("user@example.com", "User", "stored-hash");
        _users.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("wrong", "stored-hash").Returns(false);

        await Should.ThrowAsync<AuthenticationException>(
            () => _sut.LoginAsync(new LoginRequest("user@example.com", "wrong"), default));
    }

    [Fact]
    public async Task Login_with_unknown_email_fails_with_same_authentication_error()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await Should.ThrowAsync<AuthenticationException>(
            () => _sut.LoginAsync(new LoginRequest("nobody@example.com", "whatever"), default));
    }
}
