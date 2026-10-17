namespace WardenId.Identity.Domain.Tests;

public class RecoveryCodeTests
{
    private static readonly DateTimeOffset Now = TestMfa.Now;

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

        var result = code.Consume(Now);

        Assert.True(result.IsSuccess);
        Assert.True(code.IsUsed);
        Assert.Equal(Now, code.UsedAtUtc);
    }

    [Fact]
    public void Consume_FailsWhenAlreadyUsed()
    {
        var code = RecoveryCode.Create("hash-1");
        code.Consume(Now);

        var result = code.Consume(Now.AddMinutes(1));

        Assert.True(result.IsFailed);
        Assert.Equal(Now, code.UsedAtUtc);
    }
}
