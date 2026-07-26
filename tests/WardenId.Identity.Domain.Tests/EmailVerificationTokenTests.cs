namespace WardenId.Identity.Domain.Tests;

public class EmailVerificationTokenTests
{
    [Fact]
    public void Matches_TrueForSameHash()
    {
        var token = EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1));

        Assert.True(token.Matches("hash-1"));
        Assert.False(token.Matches("hash-2"));
    }

    [Fact]
    public void Consume_SucceedsWhenNotExpiredOrConsumed()
    {
        var token = EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1));

        var result = token.Consume(DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.NotNull(token.ConsumedAtUtc);
    }

    [Fact]
    public void Consume_FailsWhenExpired()
    {
        var token = EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(-1));

        var result = token.Consume(DateTimeOffset.UtcNow);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Consume_FailsWhenAlreadyConsumed()
    {
        var token = EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1));
        token.Consume(DateTimeOffset.UtcNow);

        var result = token.Consume(DateTimeOffset.UtcNow);

        Assert.True(result.IsFailed);
    }
}
