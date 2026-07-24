namespace WardenId.ClientManagement.Domain.Tests;

public class TokenLifetimeOverridesTests
{
    [Fact]
    public void Create_FailsWhenAccessTokenSecondsIsNotPositive()
    {
        var result = TokenLifetimeOverrides.Create(0, null, null);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenIdTokenSecondsIsNotPositive()
    {
        var result = TokenLifetimeOverrides.Create(null, -5, null);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenRefreshTokenSecondsIsNotPositive()
    {
        var result = TokenLifetimeOverrides.Create(null, null, 0);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SucceedsWhenAllNull()
    {
        var result = TokenLifetimeOverrides.Create(null, null, null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_SucceedsWithPositiveValues()
    {
        var result = TokenLifetimeOverrides.Create(3600, 3600, 86400);

        Assert.True(result.IsSuccess);
        Assert.Equal(3600, result.Value.AccessTokenSeconds);
        Assert.Equal(86400, result.Value.RefreshTokenSeconds);
    }
}
