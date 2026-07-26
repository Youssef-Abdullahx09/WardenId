namespace WardenId.Identity.Domain.Tests;

public class PasswordPolicyTests
{
    [Fact]
    public void Create_FailsWhenMinLengthLessThanOne()
    {
        var result = PasswordPolicy.Create(0, true, true, true, true, 5);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenHistoryCountNegative()
    {
        var result = PasswordPolicy.Create(8, true, true, true, true, -1);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SucceedsWithValidValues()
    {
        var result = PasswordPolicy.Create(12, true, true, false, true, 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value.MinLength);
        Assert.Equal(5, result.Value.HistoryCount);
        Assert.False(result.Value.RequireSymbol);
    }

    [Fact]
    public void FromTrustedSource_SkipsValidation()
    {
        var policy = PasswordPolicy.FromTrustedSource(8, false, false, false, false, 0);

        Assert.Equal(8, policy.MinLength);
    }
}
