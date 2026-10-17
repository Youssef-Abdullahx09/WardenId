namespace WardenId.Identity.Domain.Tests;

public class MfaFactorTests
{
    private static readonly DateTimeOffset Now = TestMfa.Now;

    [Fact]
    public void Create_StartsDisabledAndUnverified()
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, "protected-secret", Now);

        Assert.False(factor.Enabled);
        Assert.True(factor.IsPendingVerification);
        Assert.Null(factor.VerifiedAtUtc);
        Assert.Null(factor.LastUsedAtUtc);
        Assert.Null(factor.LastAcceptedTimeStep);
        Assert.Equal(Now, factor.EnrolledAtUtc);
        Assert.Equal(MfaFactorType.Totp, factor.FactorType);
    }

    [Fact]
    public void Verify_EnablesTheFactor_AndRecordsTheFirstCodesStep()
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, "protected-secret", Now);
        var later = Now.AddMinutes(1);

        var result = factor.Verify(timeStep: 100, later);

        Assert.True(result.IsSuccess);
        Assert.True(factor.Enabled);
        Assert.Equal(later, factor.VerifiedAtUtc);
        Assert.Equal(later, factor.LastUsedAtUtc);
        Assert.Equal(100, factor.LastAcceptedTimeStep);
    }

    [Fact]
    public void Verify_FailsWhenAlreadyVerified()
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, "protected-secret", Now);
        factor.Verify(100, Now);

        var result = factor.Verify(101, Now);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void AcceptCode_RecordsANewerStep()
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, "protected-secret", Now);
        factor.Verify(100, Now);
        var later = Now.AddMinutes(5);

        var result = factor.AcceptCode(110, later);

        Assert.True(result.IsSuccess);
        Assert.Equal(110, factor.LastAcceptedTimeStep);
        Assert.Equal(later, factor.LastUsedAtUtc);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(99)]
    public void AcceptCode_RefusesTheLastStepOrAnOlderOne(long timeStep)
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, "protected-secret", Now);
        factor.Verify(100, Now);

        var result = factor.AcceptCode(timeStep, Now.AddMinutes(1));

        Assert.True(result.IsFailed);
        Assert.False(factor.CanAcceptTimeStep(timeStep));
        Assert.Equal(100, factor.LastAcceptedTimeStep);
        Assert.Equal(Now, factor.LastUsedAtUtc);
    }

    [Fact]
    public void AcceptCode_FailsForAnUnverifiedFactor()
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, "protected-secret", Now);

        Assert.True(factor.AcceptCode(100, Now).IsFailed);
    }
}
