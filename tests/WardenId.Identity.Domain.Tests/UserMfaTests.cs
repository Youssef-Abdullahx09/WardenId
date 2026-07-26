namespace WardenId.Identity.Domain.Tests;

public class UserMfaTests
{
    private static User CreateUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    [Fact]
    public void EnrollMfaFactor_AddsFactorAndRecoveryCodes()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        var codes = new[] { RecoveryCode.Create("hash-1"), RecoveryCode.Create("hash-2") };

        var result = user.EnrollMfaFactor(factor, codes);

        Assert.True(result.IsSuccess);
        Assert.Single(user.MfaFactors);
        Assert.Equal(2, user.RecoveryCodes.Count);
    }

    [Fact]
    public void EnrollMfaFactor_RaisesMfaFactorEnrolledDomainEvent()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");

        user.EnrollMfaFactor(factor, []);

        Assert.Contains(user.DomainEvents, e => e is MfaFactorEnrolledDomainEvent d && d.MfaFactorId == factor.Id);
    }

    [Fact]
    public void RevokeMfaFactor_FailsWhenFactorNotFound()
    {
        var user = CreateUser();

        var result = user.RevokeMfaFactor(Guid.NewGuid(), MfaPolicy.Create(MfaPolicyMode.Off), userIsAdmin: false);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void RevokeMfaFactor_SucceedsWhenPolicyDoesNotRequireMfa()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, []);

        var result = user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Optional), userIsAdmin: false);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.MfaFactors);
    }

    [Fact]
    public void RevokeMfaFactor_FailsWhenItIsTheLastFactorAndPolicyRequiresMfa()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        factor.Verify();
        user.EnrollMfaFactor(factor, []);

        var result = user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Required), userIsAdmin: false);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void RevokeMfaFactor_RaisesMfaFactorRevokedDomainEventOnSuccess()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, []);

        user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Off), userIsAdmin: false);

        Assert.Contains(user.DomainEvents, e => e is MfaFactorRevokedDomainEvent d && d.MfaFactorId == factor.Id);
    }
}
