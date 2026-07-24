namespace WardenId.ClientManagement.Domain.Tests;

public class GrantTypeTests
{
    [Fact]
    public void Values_AreBitFlags_AndCanBeCombinedWithoutOverlap()
    {
        var combined = GrantType.AuthorizationCode | GrantType.RefreshToken;

        Assert.True(combined.HasFlag(GrantType.AuthorizationCode));
        Assert.True(combined.HasFlag(GrantType.RefreshToken));
        Assert.False(combined.HasFlag(GrantType.ClientCredentials));
    }
}
