namespace WardenId.Identity.Domain.Tests;

public class UserMfaTests
{
    private static readonly DateTimeOffset Now = TestMfa.Now;

    private static User CreateUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    [Fact]
    public void BeginMfaEnrollment_AddsAnUnverifiedFactor_ThatDoesNotCountAsASecondFactor()
    {
        var user = CreateUser();

        var factor = user.AddPendingTotp();

        Assert.Same(factor, Assert.Single(user.MfaFactors));
        Assert.False(user.HasEnabledMfaFactor);
    }

    [Fact]
    public void BeginMfaEnrollment_ReplacesAnUnverifiedFactor_AndKeepsEnabledOnes()
    {
        var user = CreateUser();
        var enabled = user.AddEnabledTotp();
        var abandoned = user.AddPendingTotp("abandoned");

        var latest = user.AddPendingTotp("latest");

        Assert.Equal([enabled.Id, latest.Id], user.MfaFactors.Select(f => f.Id));
        Assert.DoesNotContain(user.MfaFactors, f => f.Id == abandoned.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BeginMfaEnrollment_FailsForADisabledOrDeletedUser(bool deleted)
    {
        var user = CreateUser();
        if (deleted) user.SoftDelete();
        else user.Disable("policy violation");

        var result = user.BeginMfaEnrollment(MfaFactor.Create(MfaFactorType.Totp, "protected-secret", Now));

        Assert.True(result.IsFailed);
        Assert.Empty(user.MfaFactors);
    }

    [Fact]
    public void CompleteMfaEnrollment_EnablesTheFactor_ClearsEnrollMfa_AndReplacesRecoveryCodes()
    {
        var user = CreateUser();
        user.AddEnabledTotp([RecoveryCode.Create("old-1"), RecoveryCode.Create("old-2")]);
        user.AddRequiredAction(RequiredAction.EnrollMfa);
        var factor = user.AddPendingTotp("second");

        var result = user.CompleteMfaEnrollment(factor.Id, 200, [RecoveryCode.Create("new-1")], Now);

        Assert.True(result.IsSuccess);
        Assert.True(factor.Enabled);
        Assert.Equal(200, factor.LastAcceptedTimeStep);
        Assert.DoesNotContain(RequiredAction.EnrollMfa, user.PendingActions);
        Assert.Equal(["new-1"], user.RecoveryCodes.Select(c => c.CodeHash));
        Assert.Contains(user.DomainEvents, e => e is MfaFactorEnrolledDomainEvent d && d.MfaFactorId == factor.Id);
    }

    [Fact]
    public void CompleteMfaEnrollment_FailsForAnUnknownOrEnabledFactor()
    {
        var user = CreateUser();
        var enabled = user.AddEnabledTotp();

        Assert.True(user.CompleteMfaEnrollment(Guid.NewGuid(), 1, [], Now).IsFailed);
        Assert.True(user.CompleteMfaEnrollment(enabled.Id, 1, [], Now).IsFailed);
    }

    [Fact]
    public void AcceptMfaCode_RefusesAReplayedStep()
    {
        var user = CreateUser();
        var factor = user.AddEnabledTotp(timeStep: 100);

        Assert.True(user.AcceptMfaCode(factor.Id, 101, Now).IsSuccess);
        Assert.True(user.AcceptMfaCode(factor.Id, 101, Now).IsFailed);
    }

    [Fact]
    public void ConsumeRecoveryCode_UsesTheCodeOnce_AndCountsTheRest()
    {
        var user = CreateUser();
        user.AddEnabledTotp([RecoveryCode.Create("hash-1"), RecoveryCode.Create("hash-2")]);

        Assert.True(user.ConsumeRecoveryCode("hash-1", Now).IsSuccess);
        Assert.True(user.ConsumeRecoveryCode("hash-1", Now).IsFailed);
        Assert.Equal(1, user.RemainingRecoveryCodeCount);
        Assert.Contains(user.DomainEvents, e => e is RecoveryCodeUsedDomainEvent { RemainingRecoveryCodes: 1 });
    }

    [Fact]
    public void ConsumeRecoveryCode_FailsWhenNoMatch()
    {
        var user = CreateUser();
        user.AddEnabledTotp([RecoveryCode.Create("hash-1")]);

        Assert.True(user.ConsumeRecoveryCode("no-match", Now).IsFailed);
        Assert.Equal(1, user.RemainingRecoveryCodeCount);
    }

    [Fact]
    public void ResetMfa_RemovesEveryFactorAndCode_AndAsksForEnrollment()
    {
        var user = CreateUser();
        user.AddEnabledTotp([RecoveryCode.Create("hash-1"), RecoveryCode.Create("hash-2")]);
        user.AddPendingTotp("pending");

        var result = user.ResetMfa();

        Assert.True(result.IsSuccess);
        Assert.Empty(user.MfaFactors);
        Assert.Empty(user.RecoveryCodes);
        Assert.Contains(RequiredAction.EnrollMfa, user.PendingActions);
        Assert.Contains(user.DomainEvents, e => e is MfaResetDomainEvent { RemovedFactorCount: 2, RemovedRecoveryCodeCount: 2 });
    }

    [Fact]
    public void ResetMfa_FailsForADisabledUser()
    {
        var user = CreateUser();
        user.AddEnabledTotp();
        user.Disable("policy violation");

        Assert.True(user.ResetMfa().IsFailed);
        Assert.Single(user.MfaFactors);
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
        var factor = user.AddEnabledTotp();

        var result = user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Optional));

        Assert.True(result.IsSuccess);
        Assert.Empty(user.MfaFactors);
    }

    [Fact]
    public void RevokeMfaFactor_FailsWhenItIsTheLastFactorAndPolicyRequiresMfa()
    {
        var user = CreateUser();
        var factor = user.AddEnabledTotp();

        var result = user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Required));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void RevokeMfaFactor_RaisesMfaFactorRevokedDomainEventOnSuccess()
    {
        var user = CreateUser();
        var factor = user.AddEnabledTotp();

        user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Off));

        Assert.Contains(user.DomainEvents, e => e is MfaFactorRevokedDomainEvent d && d.MfaFactorId == factor.Id);
    }

    [Fact]
    public void RevokeMfaFactor_ClearsRecoveryCodesWhenNoEnabledFactorsRemain()
    {
        var user = CreateUser();
        var factor = user.AddEnabledTotp([RecoveryCode.Create("hash-1"), RecoveryCode.Create("hash-2")]);

        var result = user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Off));

        Assert.True(result.IsSuccess);
        Assert.Empty(user.RecoveryCodes);
    }

    [Fact]
    public void RevokeMfaFactor_KeepsRecoveryCodesWhenAnotherEnabledFactorRemains()
    {
        var user = CreateUser();
        var factor1 = user.AddEnabledTotp(credentialData: "secret-1");
        user.AddEnabledTotp([RecoveryCode.Create("hash-1")], "secret-2");

        var result = user.RevokeMfaFactor(factor1.Id, MfaPolicy.Create(MfaPolicyMode.Off));

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(user.RecoveryCodes);
    }
}
