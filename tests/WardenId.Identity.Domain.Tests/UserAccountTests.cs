namespace WardenId.Identity.Domain.Tests;

/// <summary>What the user's own account page and the accept-terms sign-in step change.</summary>
public class UserAccountTests
{
    private static readonly DateTimeOffset Now = TestMfa.Now;
    private static readonly MfaPolicy Optional = MfaPolicy.Create(MfaPolicyMode.Optional);

    private static User CreateUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    [Fact]
    public void AcceptTerms_StoresTheTime_ClearsAcceptTos_AndRaisesAnEvent()
    {
        var user = CreateUser();
        user.AddRequiredAction(RequiredAction.AcceptTos);
        user.ClearDomainEvents();

        var result = user.AcceptTerms(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now, user.TermsAcceptedAtUtc);
        Assert.DoesNotContain(RequiredAction.AcceptTos, user.PendingActions);
        Assert.Contains(RequiredAction.VerifyEmail, user.PendingActions);
        Assert.Equal(Now, Assert.IsType<UserTermsAcceptedDomainEvent>(Assert.Single(user.DomainEvents)).AcceptedAtUtc);
    }

    [Fact]
    public void AcceptTerms_Again_MovesTheTimeForward()
    {
        var user = CreateUser();
        user.AcceptTerms(Now);

        Assert.True(user.AcceptTerms(Now.AddDays(1)).IsSuccess);

        Assert.Equal(Now.AddDays(1), user.TermsAcceptedAtUtc);
    }

    [Fact]
    public void AcceptTerms_IsRefused_ForADisabledUser()
    {
        var user = CreateUser();
        user.Disable("reason");

        Assert.True(user.AcceptTerms(Now).IsFailed);
        Assert.Null(user.TermsAcceptedAtUtc);
    }

    [Fact]
    public void RemoveMfa_RemovesEveryFactorAndCode_WithoutAskingForANewEnrollment()
    {
        var user = CreateUser();
        user.AddEnabledTotp([RecoveryCode.Create("a"), RecoveryCode.Create("b")]);
        user.AddPendingTotp("pending");
        user.ClearDomainEvents();

        var result = user.RemoveMfa(Optional);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.MfaFactors);
        Assert.Empty(user.RecoveryCodes);
        Assert.DoesNotContain(RequiredAction.EnrollMfa, user.PendingActions);
        Assert.Contains(user.DomainEvents, e => e is MfaRemovedDomainEvent { RemovedFactorCount: 2, RemovedRecoveryCodeCount: 2 });
    }

    [Theory]
    [InlineData(MfaPolicyMode.Required)]
    [InlineData(MfaPolicyMode.RequiredForAdmins)]
    public void RemoveMfa_IsRefused_WhileThePolicyRequiresMfa(MfaPolicyMode mode)
    {
        var user = CreateUser();
        user.AddEnabledTotp();

        var result = user.RemoveMfa(MfaPolicy.Create(mode));

        Assert.Equal(UserErrors.MfaRequiredCannotRemove().Message, Assert.Single(result.Errors).Message);
        Assert.Single(user.MfaFactors);
    }

    [Fact]
    public void RemoveOtherMfaFactors_KeepsOnlyTheGivenOne_AndReportsEnabledOnesRevoked()
    {
        var user = CreateUser();
        var old = user.AddEnabledTotp(credentialData: "old");
        var replacement = user.AddPendingTotp("new");
        user.ClearDomainEvents();

        var result = user.RemoveOtherMfaFactors(replacement.Id);

        Assert.True(result.IsSuccess);
        Assert.Same(replacement, Assert.Single(user.MfaFactors));
        Assert.Equal(old.Id, Assert.IsType<MfaFactorRevokedDomainEvent>(Assert.Single(user.DomainEvents)).MfaFactorId);
    }

    [Fact]
    public void RemoveOtherMfaFactors_Fails_ForAnUnknownFactor()
    {
        var user = CreateUser();
        user.AddEnabledTotp();

        Assert.True(user.RemoveOtherMfaFactors(Guid.NewGuid()).IsFailed);
        Assert.Single(user.MfaFactors);
    }

    [Fact]
    public void RegenerateRecoveryCodes_ReplacesEveryCode()
    {
        var user = CreateUser();
        user.AddEnabledTotp([RecoveryCode.Create("old")]);
        user.ClearDomainEvents();

        var result = user.RegenerateRecoveryCodes([RecoveryCode.Create("new-1"), RecoveryCode.Create("new-2")]);

        Assert.True(result.IsSuccess);
        Assert.Equal(["new-1", "new-2"], user.RecoveryCodes.Select(c => c.CodeHash));
        Assert.Equal(2, Assert.IsType<RecoveryCodesRegeneratedDomainEvent>(Assert.Single(user.DomainEvents)).Count);
    }

    [Fact]
    public void RegenerateRecoveryCodes_IsRefused_WithoutAnEnabledFactor()
    {
        var user = CreateUser();
        user.AddPendingTotp();

        Assert.True(user.RegenerateRecoveryCodes([RecoveryCode.Create("new")]).IsFailed);
        Assert.Empty(user.RecoveryCodes);
    }
}
