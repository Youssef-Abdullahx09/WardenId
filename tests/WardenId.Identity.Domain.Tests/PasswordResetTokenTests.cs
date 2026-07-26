namespace WardenId.Identity.Domain.Tests;

public class PasswordResetTokenTests
{
    [Fact]
    public void Matches_TrueForSameHash()
    {
        var token = PasswordResetToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1));

        Assert.True(token.Matches("hash-1"));
    }

    [Fact]
    public void Consume_FailsWhenExpired()
    {
        var token = PasswordResetToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(-1));

        var result = token.Consume(DateTimeOffset.UtcNow);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Consume_SucceedsWhenValid()
    {
        var token = PasswordResetToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1));

        var result = token.Consume(DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
    }
}
