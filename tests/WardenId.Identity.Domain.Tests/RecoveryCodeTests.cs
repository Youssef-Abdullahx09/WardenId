namespace WardenId.Identity.Domain.Tests;

public class RecoveryCodeTests
{
    [Fact]
    public void Matches_TrueForSameHash()
    {
        var code = RecoveryCode.Create("hash-1");

        Assert.True(code.Matches("hash-1"));
        Assert.False(code.Matches("hash-2"));
    }

    [Fact]
    public void Consume_SucceedsFirstTime()
    {
        var code = RecoveryCode.Create("hash-1");

        var result = code.Consume();

        Assert.True(result.IsSuccess);
        Assert.NotNull(code.UsedAtUtc);
    }

    [Fact]
    public void Consume_FailsWhenAlreadyUsed()
    {
        var code = RecoveryCode.Create("hash-1");
        code.Consume();

        var result = code.Consume();

        Assert.True(result.IsFailed);
    }
}
