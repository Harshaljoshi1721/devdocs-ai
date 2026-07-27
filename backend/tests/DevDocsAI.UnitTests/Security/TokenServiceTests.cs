using DevDocsAI.Domain.Entities;
using DevDocsAI.Infrastructure.Security;
using DevDocsAI.UnitTests.Support;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Security;

public sealed class TokenServiceTests
{
    private readonly TestClock _clock = new();
    private readonly TokenService _sut;

    public TokenServiceTests()
    {
        var options = Options.Create(new JwtOptions
        {
            SigningKey = "unit-test-signing-key-0123456789-abcdef",
            Issuer = "devdocs-ai",
            Audience = "devdocs-ai",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 14,
        });
        _sut = new TokenService(options, _clock);
    }

    [Fact]
    public void Access_token_is_issued_with_configured_expiry()
    {
        var user = new User("user@example.com", "User", "hash");

        var token = _sut.CreateAccessToken(user);

        token.Value.ShouldNotBeNullOrWhiteSpace();
        token.ExpiresAt.ShouldBe(_clock.UtcNow.AddMinutes(15));
    }

    [Fact]
    public void Refresh_token_raw_differs_from_hash_and_is_deterministically_hashable()
    {
        var refresh = _sut.CreateRefreshToken();

        refresh.RawValue.ShouldNotBeNullOrWhiteSpace();
        refresh.Hash.ShouldNotBe(refresh.RawValue);
        refresh.ExpiresAt.ShouldBe(_clock.UtcNow.AddDays(14));
        _sut.HashRefreshToken(refresh.RawValue).ShouldBe(refresh.Hash);
    }

    [Fact]
    public void Refresh_tokens_are_unique()
    {
        _sut.CreateRefreshToken().RawValue.ShouldNotBe(_sut.CreateRefreshToken().RawValue);
    }
}
