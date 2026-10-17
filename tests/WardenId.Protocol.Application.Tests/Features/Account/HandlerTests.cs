using Microsoft.Extensions.Time.Testing;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using WardenId.Identity.Integration.Command.Features.Users.GetAccount;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using AcceptTermsCommand = WardenId.Protocol.Application.Features.Authorize.AcceptTerms.Command;
using AcceptTermsHandler = WardenId.Protocol.Application.Features.Authorize.AcceptTerms.Handler;
using BeginReplacementCommand = WardenId.Protocol.Application.Features.Account.BeginTotpReplacement.Command;
using BeginReplacementHandler = WardenId.Protocol.Application.Features.Account.BeginTotpReplacement.Handler;
using ChangePasswordCommand = WardenId.Protocol.Application.Features.Account.ChangePassword.Command;
using ChangePasswordHandler = WardenId.Protocol.Application.Features.Account.ChangePassword.Handler;
using CompleteSetupCommand = WardenId.Protocol.Application.Features.Account.CompleteTotpSetup.Command;
using CompleteSetupHandler = WardenId.Protocol.Application.Features.Account.CompleteTotpSetup.Handler;
using GetHandler = WardenId.Protocol.Application.Features.Account.Get.Handler;
using GetQuery = WardenId.Protocol.Application.Features.Account.Get.Query;
using IdentityAcceptTermsCommand = WardenId.Identity.Integration.Command.Features.Users.AcceptTerms.Command;
using IdentityBeginReplacementCommand = WardenId.Identity.Integration.Command.Features.Users.BeginTotpReplacement.Command;
using IdentityChangeOwnPasswordCommand = WardenId.Identity.Integration.Command.Features.Users.ChangeOwnPassword.Command;
using IdentityCompleteCommand = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.Command;
using IdentityCompleted = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.CompletedTotpEnrollment;
using IdentityEnrollment = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.TotpEnrollment;
using IdentityGetAccountQuery = WardenId.Identity.Integration.Command.Features.Users.GetAccount.Query;
using IdentityRegenerateCommand = WardenId.Identity.Integration.Command.Features.Users.RegenerateRecoveryCodes.Command;
using IdentityRemoveMfaCommand = WardenId.Identity.Integration.Command.Features.Users.RemoveMfa.Command;
using RegenerateCommand = WardenId.Protocol.Application.Features.Account.RegenerateRecoveryCodes.Command;
using RegenerateHandler = WardenId.Protocol.Application.Features.Account.RegenerateRecoveryCodes.Handler;
using RemoveMfaCommand = WardenId.Protocol.Application.Features.Account.RemoveMfa.Command;
using RemoveMfaHandler = WardenId.Protocol.Application.Features.Account.RemoveMfa.Handler;
using EndForUserCommand = WardenId.Protocol.Application.Features.Sessions.EndForUser.Command;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;
using SignInUser = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.SignInUser;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;
using Domain = WardenId.Protocol.Domain;

namespace WardenId.Protocol.Application.Tests.Features.Account;

/// <summary>The account and terms handlers: thin over Identity, but they decide what the user is told.</summary>
public class HandlerTests
{
    private const string WrongCode = "That code didn't work. Please try again.";

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid FactorId = Guid.NewGuid();
    private static readonly Guid Sid = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-17T09:00:00Z");

    private readonly FakeAuthAuditTrail _audit = new();
    private readonly FakeTimeProvider _clock = new(Now);
    private static readonly DateTimeOffset Enrolled = DateTimeOffset.Parse("2026-10-01T08:00:00Z");

    private static Error Tagged(SecondFactorFailureReason reason) =>
        SecondFactorFailure.Tag(new Error("Invalid code."), reason);

    private static AccountView View(string? termsUrl) =>
        new("alice@acme.test", "alice", null, termsUrl, MfaAvailable: true, MfaRequired: false,
            [new AccountFactor(FactorId, "Totp", Enrolled, null)], 7);

    [Fact]
    public async Task Get_MapsTheAccount_ForTheGivenUser()
    {
        var sender = new StubSender().Returns<IdentityGetAccountQuery, Result<AccountView>>(
            Result.Ok(View("https://acme.example/terms")));

        var result = await new GetHandler(sender).Handle(new GetQuery(TenantId, UserId), CancellationToken.None);

        Assert.Equal("alice", result.Value.Username);
        Assert.Equal("https://acme.example/terms", result.Value.TermsUrl);
        Assert.True(result.Value.HasMfa);
        Assert.Equal(7, result.Value.RemainingRecoveryCodes);
        Assert.Equal(Enrolled, Assert.Single(result.Value.Factors).EnrolledAtUtc);
        var sent = Assert.IsType<IdentityGetAccountQuery>(Assert.Single(sender.Sent));
        Assert.Equal((TenantId, UserId), (sent.TenantId, sent.UserId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("/relative/terms")]
    [InlineData("ftp://acme.example/terms")]
    public async Task Get_DropsATermsUrlThatIsNotAWebLink(string? termsUrl)
    {
        var sender = new StubSender().Returns<IdentityGetAccountQuery, Result<AccountView>>(Result.Ok(View(termsUrl)));

        var result = await new GetHandler(sender).Handle(new GetQuery(TenantId, UserId), CancellationToken.None);

        Assert.Null(result.Value.TermsUrl);
    }

    [Fact]
    public async Task AcceptTerms_PassesTheUser_AndIsAuditedAsTheUsersAcceptance()
    {
        var ok = new StubSender().Returns<IdentityAcceptTermsCommand, Result>(Result.Ok());

        var result = await new AcceptTermsHandler(ok, _audit, _clock).Handle(new AcceptTermsCommand(TenantId, UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserId, Assert.IsType<IdentityAcceptTermsCommand>(Assert.Single(ok.Sent)).UserId);
        var recorded = Assert.Single(_audit.Recorded);
        AssertUserChange(recorded, SecurityEvent.TermsAccepted, sid: null);
    }

    [Fact]
    public async Task AcceptTerms_Failure_IsAnsweredGenerically_AndNotAudited()
    {
        var failing = new StubSender().Returns<IdentityAcceptTermsCommand, Result>(Result.Fail("db down: secret detail"));

        var result = await new AcceptTermsHandler(failing, _audit, _clock).Handle(new AcceptTermsCommand(TenantId, UserId), CancellationToken.None);

        Assert.DoesNotContain("secret detail", result.Errors[0].Message);
        Assert.Empty(_audit.Recorded);
    }

    // --- Change password ---

    [Fact]
    public async Task ChangePassword_PolicyRefusal_PassesIdentitysMessageThrough_AndIsNotAudited()
    {
        var sender = new StubSender().Returns<IdentityChangeOwnPasswordCommand, Result>(
            Result.Fail("Password must be at least 12 characters."));

        var result = await new ChangePasswordHandler(sender, _audit, _clock)
            .Handle(new ChangePasswordCommand(TenantId, UserId, Sid, "old", "new"), CancellationToken.None);

        Assert.Equal("Password must be at least 12 characters.", result.Errors[0].Message);
        var sent = Assert.IsType<IdentityChangeOwnPasswordCommand>(Assert.Single(sender.Sent));
        Assert.Equal(("old", "new"), (sent.CurrentPassword, sent.NewPassword));
        Assert.Empty(_audit.Recorded);
    }

    [Fact]
    public async Task ChangePassword_Success_IsAuditedAsThePasswordChange()
    {
        var sender = new StubSender().Returns<IdentityChangeOwnPasswordCommand, Result>(Result.Ok());

        var result = await new ChangePasswordHandler(sender, _audit, _clock)
            .Handle(new ChangePasswordCommand(TenantId, UserId, Sid, "old password", "new password"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertUserChange(Assert.Single(_audit.Recorded), SecurityEvent.PasswordChanged, Sid);
    }

    [Theory]
    [InlineData(FailureReason.InvalidPassword, OutcomeReason.InvalidPassword)]
    [InlineData(FailureReason.LockedOut, OutcomeReason.AccountLockedOut)]
    [InlineData(FailureReason.Locked, OutcomeReason.AccountLocked)]
    [InlineData(FailureReason.Disabled, OutcomeReason.AccountDisabled)]
    public async Task ChangePassword_RefusedCurrentPassword_IsAuditedAsAFailedStepUp(FailureReason tag, OutcomeReason reason)
    {
        var sender = new StubSender().Returns<IdentityChangeOwnPasswordCommand, Result>(
            Result.Fail(AuthenticationFailure.Tag(new Error("Invalid credentials."), tag, UserId)));

        var result = await new ChangePasswordHandler(sender, _audit, _clock)
            .Handle(new ChangePasswordCommand(TenantId, UserId, Sid, "wrong password", "new password"), CancellationToken.None);

        Assert.Equal("Invalid credentials.", result.Errors[0].Message);
        AssertStepUpFailed(Assert.Single(_audit.Recorded), reason);
    }

    // --- Authenticator set up / replaced from the account page ---

    [Fact]
    public async Task CompleteSetup_FirstAuthenticator_ReturnsTheCodes_IsAuditedAsEnrolled_AndEndsNoSession()
    {
        var sender = new StubSender().Returns<IdentityCompleteCommand, Result<IdentityCompleted>>(
            Result.Ok(new IdentityCompleted(["abcde-fghij"], ReplacedFactor: false)));

        var result = await new CompleteSetupHandler(sender, _audit, _clock)
            .Handle(new CompleteSetupCommand(TenantId, UserId, Sid, FactorId, "123456"), CancellationToken.None);

        Assert.Equal(["abcde-fghij"], result.Value);
        Assert.Equal(FactorId, Assert.IsType<IdentityCompleteCommand>(Assert.Single(sender.Sent)).FactorId);
        AssertUserChange(Assert.Single(_audit.Recorded), SecurityEvent.TotpEnrolled, Sid);
    }

    [Fact]
    public async Task CompleteSetup_Replacement_IsAuditedAsReplaced_AndEndsTheOtherSessionsInTheTenant()
    {
        var sender = new StubSender()
            .Returns<IdentityCompleteCommand, Result<IdentityCompleted>>(
                Result.Ok(new IdentityCompleted(["abcde-fghij"], ReplacedFactor: true)))
            .Returns<EndForUserCommand, Result>(Result.Ok());

        var result = await new CompleteSetupHandler(sender, _audit, _clock)
            .Handle(new CompleteSetupCommand(TenantId, UserId, Sid, FactorId, "123456"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertUserChange(Assert.Single(_audit.Recorded), SecurityEvent.AuthenticatorReplaced, Sid);
        AssertOtherSessionsEnded(sender);
    }

    [Fact]
    public async Task CompleteSetup_WrongCode_IsAnsweredGenerically_AndAuditedAsAFailedStepUp()
    {
        var sender = new StubSender().Returns<IdentityCompleteCommand, Result<IdentityCompleted>>(
            Result.Fail<IdentityCompleted>("Invalid code."));

        var result = await new CompleteSetupHandler(sender, _audit, _clock)
            .Handle(new CompleteSetupCommand(TenantId, UserId, Sid, FactorId, "000000"), CancellationToken.None);

        Assert.Equal(WrongCode, result.Errors.Single().Message);
        AssertStepUpFailed(Assert.Single(_audit.Recorded), OutcomeReason.InvalidSecondFactor);
        Assert.DoesNotContain(sender.Sent, r => r is EndForUserCommand);
    }

    [Fact]
    public async Task BeginReplacement_PassesCodeAndIssuer_ReturnsTheNewSecret_AndAuditsNothingYet()
    {
        var sender = new StubSender().Returns<IdentityBeginReplacementCommand, Result<IdentityEnrollment>>(
            Result.Ok(new IdentityEnrollment(FactorId, "JBSWY3DPEHPK3PXP", "otpauth://totp/acme:alice")));

        var result = await new BeginReplacementHandler(sender, _audit, _clock)
            .Handle(new BeginReplacementCommand(TenantId, UserId, Sid, "123456", "acme"), CancellationToken.None);

        Assert.Equal(FactorId, result.Value.FactorId);
        var sent = Assert.IsType<IdentityBeginReplacementCommand>(Assert.Single(sender.Sent));
        Assert.Equal(("123456", "acme"), (sent.Code, sent.Issuer));
        Assert.Empty(_audit.Recorded);
    }

    // --- Step-ups in front of replace, new recovery codes and remove ---

    [Theory]
    [InlineData(SecondFactorFailureReason.InvalidCode, OutcomeReason.InvalidSecondFactor)]
    [InlineData(SecondFactorFailureReason.LockedOut, OutcomeReason.AccountLockedOut)]
    [InlineData(SecondFactorFailureReason.AccountInactive, OutcomeReason.AccountDisabled)]
    public async Task StepUpFailures_AreAllAnsweredWithTheSameMessage_AndAuditedWithTheRealReason(
        SecondFactorFailureReason tag, OutcomeReason reason)
    {
        var sender = new StubSender()
            .Returns<IdentityBeginReplacementCommand, Result<IdentityEnrollment>>(Result.Fail<IdentityEnrollment>(Tagged(tag)))
            .Returns<IdentityRegenerateCommand, Result<IReadOnlyList<string>>>(Result.Fail<IReadOnlyList<string>>(Tagged(tag)))
            .Returns<IdentityRemoveMfaCommand, Result>(Result.Fail(Tagged(tag)))
            .Returns<GetForSignInQuery, Result<SignInUser>>(Result.Ok(
                new SignInUser(UserId, "alice@acme.test", "alice", UserStatus.Disabled, [])));

        var replace = await new BeginReplacementHandler(sender, _audit, _clock)
            .Handle(new BeginReplacementCommand(TenantId, UserId, Sid, "000000", "acme"), CancellationToken.None);
        var regenerate = await new RegenerateHandler(sender, _audit, _clock)
            .Handle(new RegenerateCommand(TenantId, UserId, Sid, "000000"), CancellationToken.None);
        var remove = await new RemoveMfaHandler(sender, _audit, _clock)
            .Handle(new RemoveMfaCommand(TenantId, UserId, Sid, "000000"), CancellationToken.None);

        Assert.Equal(WrongCode, replace.Errors.Single().Message);
        Assert.Equal(WrongCode, regenerate.Errors.Single().Message);
        Assert.Equal(WrongCode, remove.Errors.Single().Message);
        Assert.Equal(3, _audit.Recorded.Count);
        Assert.All(_audit.Recorded, recorded => AssertStepUpFailed(recorded, reason));
        Assert.DoesNotContain(sender.Sent, r => r is EndForUserCommand);
    }

    [Fact]
    public async Task StepUp_NotEnrolled_ChecksNoCode_AndIsNotAudited()
    {
        var sender = new StubSender().Returns<IdentityRegenerateCommand, Result<IReadOnlyList<string>>>(
            Result.Fail<IReadOnlyList<string>>(Tagged(SecondFactorFailureReason.NotEnrolled)));

        var result = await new RegenerateHandler(sender, _audit, _clock)
            .Handle(new RegenerateCommand(TenantId, UserId, Sid, "123456"), CancellationToken.None);

        Assert.Equal(WrongCode, result.Errors.Single().Message);
        Assert.Empty(_audit.Recorded);
    }

    [Fact]
    public async Task Regenerate_ReturnsTheNewCodes_AndIsAuditedWithoutThem()
    {
        var sender = new StubSender().Returns<IdentityRegenerateCommand, Result<IReadOnlyList<string>>>(
            Result.Ok<IReadOnlyList<string>>(["one", "two"]));

        var result = await new RegenerateHandler(sender, _audit, _clock)
            .Handle(new RegenerateCommand(TenantId, UserId, Sid, "123456"), CancellationToken.None);

        Assert.Equal(["one", "two"], result.Value);
        AssertUserChange(Assert.Single(_audit.Recorded), SecurityEvent.RecoveryCodesRegenerated, Sid);
    }

    [Fact]
    public async Task RemoveMfa_RefusedByPolicy_GetsAFixedMessage_AndIsNotAudited()
    {
        var sender = new StubSender().Returns<IdentityRemoveMfaCommand, Result>(
            Result.Fail("The tenant's MFA policy requires MFA for this user."));

        var result = await new RemoveMfaHandler(sender, _audit, _clock)
            .Handle(new RemoveMfaCommand(TenantId, UserId, Sid, "123456"), CancellationToken.None);

        Assert.Equal(RemoveMfaHandler.RefusedMessage, result.Errors.Single().Message);
        Assert.Empty(_audit.Recorded);
        Assert.DoesNotContain(sender.Sent, r => r is EndForUserCommand);
    }

    [Fact]
    public async Task RemoveMfa_Success_IsAudited_AndEndsTheOtherSessionsInTheTenant()
    {
        var sender = new StubSender()
            .Returns<IdentityRemoveMfaCommand, Result>(Result.Ok())
            .Returns<EndForUserCommand, Result>(Result.Ok());

        var result = await new RemoveMfaHandler(sender, _audit, _clock)
            .Handle(new RemoveMfaCommand(TenantId, UserId, Sid, "123456"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertUserChange(Assert.Single(_audit.Recorded), SecurityEvent.MfaRemoved, Sid);
        AssertOtherSessionsEnded(sender);
    }

    [Fact]
    public async Task RemoveMfa_SessionsThatCannotBeEnded_FailLoudly()
    {
        var sender = new StubSender()
            .Returns<IdentityRemoveMfaCommand, Result>(Result.Ok())
            .Returns<EndForUserCommand, Result>(Result.Fail("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new RemoveMfaHandler(sender, _audit, _clock)
            .Handle(new RemoveMfaCommand(TenantId, UserId, Sid, "123456"), CancellationToken.None));
    }

    // --- Every account command acts as its user (audit capture of Identity's writes) ---

    [Fact]
    public void AccountAndSignInStepCommands_ActAsTheirUser()
    {
        Assert.All(
            [
                new AcceptTermsCommand(TenantId, UserId),
                new ChangePasswordCommand(TenantId, UserId, Sid, "a", "b"),
                new CompleteSetupCommand(TenantId, UserId, Sid, FactorId, "1"),
                new BeginReplacementCommand(TenantId, UserId, Sid, "1", "acme"),
                new RegenerateCommand(TenantId, UserId, Sid, "1"),
                new RemoveMfaCommand(TenantId, UserId, Sid, "1"),
                new WardenId.Protocol.Application.Features.Authorize.BeginTotpEnrollment.Command(TenantId, UserId, "acme"),
                new WardenId.Protocol.Application.Features.Authorize.CompleteTotpEnrollment.Command(TenantId, UserId, FactorId, "1"),
                (object)new WardenId.Protocol.Application.Features.Authorize.VerifySecondFactor.Command(TenantId, UserId, "1")
            ],
            command =>
            {
                var acting = Assert.IsAssignableFrom<WardenId.Protocol.Application.Auditing.IActsAsUser>(command);
                Assert.Equal((TenantId, UserId), (acting.TenantId, acting.UserId));
            });
    }

    private void AssertUserChange(RecordAuditEventCommand recorded, SecurityEvent change, Guid? sid)
    {
        Assert.Equal(change, recorded.Event);
        Assert.Equal(TenantId, recorded.TenantId);
        Assert.Equal(Now, recorded.OccurredAt);
        Assert.Equal(WardenId.Audit.Integration.Command.Features.Events.Record.Actor.User(UserId), recorded.Actor);
        Assert.Equal([Target.User(UserId)], recorded.Targets);
        Assert.Null(recorded.Reason);
        Assert.Equal(sid, recorded.Session?.Sid);
    }

    private void AssertStepUpFailed(RecordAuditEventCommand recorded, OutcomeReason reason)
    {
        Assert.Equal(SecurityEvent.StepUpFailed, recorded.Event);
        Assert.Equal(reason, recorded.Reason);
        Assert.Equal(TenantId, recorded.TenantId);
        Assert.Equal(WardenId.Audit.Integration.Command.Features.Events.Record.Actor.User(UserId), recorded.Actor);
        Assert.Equal([Target.User(UserId)], recorded.Targets);
        Assert.Equal(Sid, recorded.Session?.Sid);
    }

    private static void AssertOtherSessionsEnded(StubSender sender)
    {
        var end = Assert.Single(sender.Sent.OfType<EndForUserCommand>());
        Assert.Equal(UserId, end.UserId);
        Assert.Equal(TenantId, end.TenantId);
        Assert.Equal(Sid, end.ExceptSid);
        Assert.Equal(Domain.Sessions.EndReason.MfaChanged, end.Reason);
    }
}
