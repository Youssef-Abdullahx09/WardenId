namespace WardenId.Identity.Domain.Tests;

public class MfaPolicyEvaluatorTests
{
    [Theory]
    [InlineData(MfaPolicyMode.Off, false, false)]
    [InlineData(MfaPolicyMode.Optional, false, false)]
    [InlineData(MfaPolicyMode.Required, false, true)]
    [InlineData(MfaPolicyMode.Required, true, true)]
    [InlineData(MfaPolicyMode.RequiredForAdmins, false, false)]
    [InlineData(MfaPolicyMode.RequiredForAdmins, true, true)]
    public void IsMfaRequired_MatchesPolicyModeAndAdminStatus(MfaPolicyMode mode, bool userIsAdmin, bool expected)
    {
        var policy = MfaPolicy.Create(mode);

        Assert.Equal(expected, MfaPolicyEvaluator.IsMfaRequired(policy, userIsAdmin));
    }

    [Fact]
    public void CanRevokeFactor_SucceedsWhenPolicyDoesNotRequireMfa()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, []);

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.Optional), user, factor, userIsAdmin: false);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CanRevokeFactor_FailsWhenItIsTheOnlyEnabledFactorAndPolicyRequiresMfa()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        factor.Verify();
        user.EnrollMfaFactor(factor, []);

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.Required), user, factor, userIsAdmin: false);

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

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.Required), user, factorA, userIsAdmin: false);

        Assert.True(result.IsSuccess);
    }
}
