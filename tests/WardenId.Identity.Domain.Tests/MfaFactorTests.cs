namespace WardenId.Identity.Domain.Tests;

public class MfaFactorTests
{
    [Fact]
    public void Create_StartsDisabledAndUnverified()
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");

        Assert.False(factor.Enabled);
        Assert.Null(factor.VerifiedAtUtc);
        Assert.Equal(MfaFactorType.Totp, factor.FactorType);
    }

    [Fact]
    public void Verify_EnablesTheFactorAndSetsVerifiedAt()
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");

        var result = factor.Verify();

        Assert.True(result.IsSuccess);
        Assert.True(factor.Enabled);
        Assert.NotNull(factor.VerifiedAtUtc);
    }

    [Fact]
    public void Verify_FailsWhenAlreadyVerified()
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        factor.Verify();

        var result = factor.Verify();

        Assert.True(result.IsFailed);
    }
}
