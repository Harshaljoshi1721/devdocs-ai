using DevDocsAI.Application.Features.Usage;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class TokenEstimatorTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abcd", 1)]
    [InlineData("abcde", 2)]
    public void Estimate_is_roughly_length_over_four(string text, int expected) =>
        TokenEstimator.Estimate(text).ShouldBe(expected);

    [Fact]
    public void Estimate_null_is_zero() => TokenEstimator.Estimate(null).ShouldBe(0);
}
