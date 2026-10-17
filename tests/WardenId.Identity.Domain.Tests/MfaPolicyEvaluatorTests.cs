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
        var factor = user.AddEnabledTotp();

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.Optional), user, factor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CanRevokeFactor_FailsWhenItIsTheOnlyEnabledFactorAndPolicyRequiresMfa()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        var factor = user.AddEnabledTotp();

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.Required), user, factor);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void CanRevokeFactor_SucceedsWhenAnotherEnabledFactorRemains()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        var factorA = user.AddEnabledTotp(credentialData: "secret-a");
        user.AddEnabledTotp(credentialData: "secret-b");

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.Required), user, factorA);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CanRevokeFactor_FailsWhenItIsTheOnlyEnabledFactorAndPolicyIsRequiredForAdmins()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        var factor = user.AddEnabledTotp();

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.RequiredForAdmins), user, factor);

        Assert.True(result.IsFailed);
    }
}
