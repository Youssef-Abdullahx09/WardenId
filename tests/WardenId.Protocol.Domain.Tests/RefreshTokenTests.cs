namespace WardenId.Protocol.Domain.Tests;

public class RefreshTokenTests
{
    [Fact]
    public void Create_Succeeds_WithValidHashAndFutureExpiry()
    {
        var result = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "somehash", DateTimeOffset.UtcNow.AddDays(30));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsRevoked);
    }

    [Fact]
    public void Create_Fails_WhenTokenHashIsEmpty()
    {
        var result = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "", DateTimeOffset.UtcNow.AddDays(30));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_Fails_WhenExpiryIsInThePast()
    {
        var result = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "somehash", DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.True(result.IsFailed);
    }
}
