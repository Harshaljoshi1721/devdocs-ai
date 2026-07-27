using DevDocsAI.Infrastructure.Security;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Security;

public sealed class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_does_not_return_plaintext_and_verifies()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        hash.ShouldNotBe("correct horse battery staple");
        _hasher.Verify("correct horse battery staple", hash).ShouldBeTrue();
    }

    [Fact]
    public void Verify_rejects_wrong_password()
    {
        var hash = _hasher.Hash("secret-one");
        _hasher.Verify("secret-two", hash).ShouldBeFalse();
    }

    [Fact]
    public void Verify_returns_false_for_malformed_hash()
    {
        _hasher.Verify("anything", "not-a-bcrypt-hash").ShouldBeFalse();
    }
}
