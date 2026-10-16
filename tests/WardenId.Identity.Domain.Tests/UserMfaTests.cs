using WardenId.Identity.Domain.Events;

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

        var result = user.RevokeMfaFactor(Guid.NewGuid(), MfaPolicy.Create(MfaPolicyMode.Off));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void RevokeMfaFactor_SucceedsWhenPolicyDoesNotRequireMfa()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, []);

        var result = user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Optional));

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

        var result = user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Required));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void RevokeMfaFactor_RaisesMfaFactorRevokedDomainEventOnSuccess()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, []);

        user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Off));

        Assert.Contains(user.DomainEvents, e => e is MfaFactorRevokedDomainEvent d && d.MfaFactorId == factor.Id);
    }

    [Fact]
    public void EnrollMfaFactor_FailsWhenUserIsSoftDeleted()
    {
        var user = CreateUser();
        user.SoftDelete();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");

        var result = user.EnrollMfaFactor(factor, []);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void EnrollMfaFactor_FailsWhenUserIsDisabled()
    {
        var user = CreateUser();
        user.Disable("policy violation");
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");

        var result = user.EnrollMfaFactor(factor, []);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void VerifyMfaFactor_EnablesTheFactor()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, []);

        var result = user.VerifyMfaFactor(factor.Id);

        Assert.True(result.IsSuccess);
        Assert.True(factor.Enabled);
    }

    [Fact]
    public void VerifyMfaFactor_FailsWhenNotFound()
    {
        var user = CreateUser();

        var result = user.VerifyMfaFactor(Guid.NewGuid());

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void ConsumeRecoveryCode_SucceedsForMatchingCode()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, [RecoveryCode.Create("hash-1")]);

        var result = user.ConsumeRecoveryCode("hash-1");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ConsumeRecoveryCode_FailsWhenNoMatch()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, [RecoveryCode.Create("hash-1")]);

        var result = user.ConsumeRecoveryCode("no-match");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void RevokeMfaFactor_ClearsRecoveryCodesWhenNoEnabledFactorsRemain()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, [RecoveryCode.Create("hash-1"), RecoveryCode.Create("hash-2")]);

        var result = user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Off));

        Assert.True(result.IsSuccess);
        Assert.Empty(user.RecoveryCodes);
    }

    [Fact]
    public void RevokeMfaFactor_KeepsRecoveryCodesWhenAnotherEnabledFactorRemains()
    {
        var user = CreateUser();
        var factor1 = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret-1");
        var factor2 = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret-2");
        user.EnrollMfaFactor(factor1, [RecoveryCode.Create("hash-1")]);
        user.EnrollMfaFactor(factor2, []);
        factor1.Verify();
        factor2.Verify();

        var result = user.RevokeMfaFactor(factor1.Id, MfaPolicy.Create(MfaPolicyMode.Off));

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(user.RecoveryCodes);
    }
}
