using WardenId.SharedKernel.Domain;

namespace WardenId.Protocol.Application.Tests;

public class TenantSlugFormatTests
{
    [Theory]
    [InlineData("acme", true)]
    [InlineData("my-co", true)]
    [InlineData("a1b", true)]
    [InlineData("ab", false)]
    [InlineData("ACME", false)]
    [InlineData("-acme", false)]
    [InlineData("acme-", false)]
    [InlineData("a_b", false)]
    [InlineData("", false)]
    public void IsValid_MatchesSlugRules(string candidate, bool expected) =>
        Assert.Equal(expected, TenantSlugFormat.IsValid(candidate));

    [Fact]
    public void IsValid_RejectsSixtyFourCharacters() =>
        Assert.False(TenantSlugFormat.IsValid(new string('a', 64)));

    [Fact]
    public void IsValid_AcceptsSixtyThreeCharacters() =>
        Assert.True(TenantSlugFormat.IsValid(new string('a', 63)));
}
