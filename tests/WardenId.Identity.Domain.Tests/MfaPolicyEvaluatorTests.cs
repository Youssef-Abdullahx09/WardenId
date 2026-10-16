namespace WardenId.Identity.Domain.Tests;

public class MfaPolicyEvaluatorTests
{
    // WardenId can't tell which users are admins (no roles), so RequiredForAdmins fails closed:
    // it requires MFA of every user rather than of none.
    [Theory]
    [InlineData(MfaPolicyMode.Off, false)]
    [InlineData(MfaPolicyMode.Optional, false)]
    [InlineData(MfaPolicyMode.Required, true)]
    [InlineData(MfaPolicyMode.RequiredForAdmins, true)]
    public void IsMfaRequired_MatchesPolicyMode(MfaPolicyMode mode, bool expected)
    {
        var policy = MfaPolicy.Create(mode);

        Assert.Equal(expected, MfaPolicyEvaluator.IsMfaRequired(policy));
    }

    [Fact]
    public void CanRevokeFactor_SucceedsWhenPolicyDoesNotRequireMfa()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, []);

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.Optional), user, factor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CanRevokeFactor_FailsWhenItIsTheOnlyEnabledFactorAndPolicyRequiresMfa()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        factor.Verify();
        user.EnrollMfaFactor(factor, []);

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.Required), user, factor);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void CanRevokeFactor_SucceedsWhenAnotherEnabledFactorRemains()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        var factorA = MfaFactor.Create(MfaFactorType.Totp, "secret-a");
        factorA.Verify();
        var factorB = MfaFactor.Create(MfaFactorType.Totp, "secret-b");
        factorB.Verify();
        user.EnrollMfaFactor(factorA, []);
        user.EnrollMfaFactor(factorB, []);

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.Required), user, factorA);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CanRevokeFactor_FailsWhenItIsTheOnlyEnabledFactorAndPolicyIsRequiredForAdmins()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        factor.Verify();
        user.EnrollMfaFactor(factor, []);

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.RequiredForAdmins), user, factor);

        Assert.True(result.IsFailed);
    }
}
