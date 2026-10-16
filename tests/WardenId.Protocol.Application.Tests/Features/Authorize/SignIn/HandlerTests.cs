using Microsoft.Extensions.Time.Testing;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using WardenId.Protocol.Application.Features.Authorize.SignIn;
using Command = WardenId.Protocol.Application.Features.Authorize.SignIn.Command;
using AuthenticatedUser = WardenId.Identity.Integration.Command.Features.Users.Authenticate.AuthenticatedUser;
using RequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Protocol.Application.Tests.Features.Authorize.SignIn;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-16T08:00:00Z");

    private static Task<Result<SignInOutcome>> SignInAsync(Result<AuthenticatedUser> authentication) =>
        SignInAsync(authentication, new FakeAuthAuditTrail());

    private static Task<Result<SignInOutcome>> SignInAsync(Result<AuthenticatedUser> authentication, FakeAuthAuditTrail audit) =>
        new Handler(new FakeSender { AuthenticateResponse = authentication }, audit, new FakeTimeProvider(Now))
            .Handle(new Command(TenantId, "alice", "pw"), CancellationToken.None);

    private static Result<AuthenticatedUser> Failed(FailureReason reason, Guid? userId) =>
        Result.Fail<AuthenticatedUser>(AuthenticationFailure.Tag(new Error("Invalid credentials."), reason, userId));

    private static Result<AuthenticatedUser> User(UserStatus status, params RequiredAction[] actions) =>
        Result.Ok(new AuthenticatedUser(UserId, "alice@acme.test", "alice", status, actions));

    [Fact]
    public async Task Handle_SignsIn_ActiveUserWithoutActions()
    {
        var result = await SignInAsync(User(UserStatus.Active));

        Assert.True(result.IsSuccess);
        Assert.Equal(UserId, result.Value.UserId);
        Assert.Null(result.Value.BlockedReason);
    }

    [Fact]
    public async Task Handle_FailsInvalidCredentials_WhenAuthenticationFails()
    {
        var result = await SignInAsync(Result.Fail<AuthenticatedUser>("Account locked."));

        Assert.Equal(ProtocolErrorCodes.InvalidCredentials, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
        Assert.Equal("Invalid username or password.", result.Errors[0].Message);
    }

    [Theory]
    [InlineData(UserStatus.Disabled)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.SoftDeleted)]
    public async Task Handle_FailsInvalidCredentials_ForUnusableStatus(UserStatus status)
    {
        var result = await SignInAsync(User(status));

        Assert.Equal(ProtocolErrorCodes.InvalidCredentials, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Theory]
    [InlineData(RequiredAction.VerifyEmail, SignInBlockReason.VerifyEmail)]
    [InlineData(RequiredAction.ResetPassword, SignInBlockReason.ResetPassword)]
    [InlineData(RequiredAction.AcceptTos, SignInBlockReason.AcceptTos)]
    [InlineData(RequiredAction.EnrollMfa, SignInBlockReason.EnrollMfa)]
    public async Task Handle_Blocks_ForEachPendingAction(RequiredAction action, SignInBlockReason expected)
    {
        var result = await SignInAsync(User(UserStatus.Active, action));

        Assert.Equal(expected, result.Value.BlockedReason);
    }

    [Fact]
    public async Task Handle_BlockReason_FollowsFixedPriority()
    {
        var result = await SignInAsync(User(UserStatus.PendingVerification, RequiredAction.EnrollMfa, RequiredAction.VerifyEmail));

        Assert.Equal(SignInBlockReason.VerifyEmail, result.Value.BlockedReason);
    }

    [Fact]
    public async Task Handle_Blocks_PendingVerificationWithoutActions()
    {
        var result = await SignInAsync(User(UserStatus.PendingVerification));

        Assert.Equal(SignInBlockReason.PendingVerification, result.Value.BlockedReason);
    }

    [Fact]
    public async Task Success_RecordsNothing_TheSessionStartIsTheSuccessEvent()
    {
        var audit = new FakeAuthAuditTrail();

        await SignInAsync(User(UserStatus.Active), audit);

        Assert.Empty(audit.Recorded);
    }

    [Fact]
    public async Task UnknownUser_RecordsAnAnonymousFailure_WithoutTargets()
    {
        var audit = new FakeAuthAuditTrail();

        await SignInAsync(Failed(FailureReason.UnknownUser, null), audit);

        var recorded = Assert.Single(audit.Recorded);
        Assert.Equal(SecurityEvent.SignInFailed, recorded.Event);
        Assert.Equal(TenantId, recorded.TenantId);
        Assert.Equal(Now, recorded.OccurredAt);
        Assert.Equal(Actor.Anonymous, recorded.Actor);
        Assert.Empty(recorded.Targets);
        Assert.Equal(OutcomeReason.UnknownUser, recorded.Reason);
    }

    [Theory]
    [InlineData(FailureReason.InvalidPassword, OutcomeReason.InvalidPassword)]
    [InlineData(FailureReason.LockedOut, OutcomeReason.AccountLockedOut)]
    [InlineData(FailureReason.Locked, OutcomeReason.AccountLocked)]
    [InlineData(FailureReason.Disabled, OutcomeReason.AccountDisabled)]
    [InlineData(FailureReason.Deleted, OutcomeReason.AccountDeleted)]
    public async Task KnownUserFailure_RecordsTheReason_WithTheUserAsActorAndTarget(FailureReason failure, OutcomeReason expected)
    {
        var audit = new FakeAuthAuditTrail();

        var result = await SignInAsync(Failed(failure, UserId), audit);

        Assert.Equal(ProtocolErrorCodes.InvalidCredentials, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
        var recorded = Assert.Single(audit.Recorded);
        Assert.Equal(SecurityEvent.SignInFailed, recorded.Event);
        Assert.Equal(expected, recorded.Reason);
        Assert.Equal(Actor.User(UserId), recorded.Actor);
        Assert.Equal([Target.User(UserId)], recorded.Targets);
    }

    [Fact]
    public async Task UnclassifiedFailure_RecordsInvalidCredentials_Anonymously()
    {
        var audit = new FakeAuthAuditTrail();

        await SignInAsync(Result.Fail<AuthenticatedUser>("boom"), audit);

        var recorded = Assert.Single(audit.Recorded);
        Assert.Equal(OutcomeReason.InvalidCredentials, recorded.Reason);
        Assert.Equal(Actor.Anonymous, recorded.Actor);
    }

    [Theory]
    [InlineData(UserStatus.Disabled, OutcomeReason.AccountDisabled)]
    [InlineData(UserStatus.Locked, OutcomeReason.AccountLocked)]
    [InlineData(UserStatus.SoftDeleted, OutcomeReason.AccountDeleted)]
    public async Task UnusableStatus_RecordsTheStatusAsReason(UserStatus status, OutcomeReason expected)
    {
        var audit = new FakeAuthAuditTrail();

        await SignInAsync(User(status), audit);

        var recorded = Assert.Single(audit.Recorded);
        Assert.Equal(expected, recorded.Reason);
        Assert.Equal(Actor.User(UserId), recorded.Actor);
    }

    [Theory]
    [InlineData(RequiredAction.VerifyEmail, OutcomeReason.EmailNotVerified)]
    [InlineData(RequiredAction.ResetPassword, OutcomeReason.PasswordResetRequired)]
    [InlineData(RequiredAction.AcceptTos, OutcomeReason.TermsNotAccepted)]
    [InlineData(RequiredAction.EnrollMfa, OutcomeReason.MfaEnrollmentRequired)]
    public async Task PendingAction_RecordsAFailure_WithTheBlockingAction(RequiredAction action, OutcomeReason expected)
    {
        var audit = new FakeAuthAuditTrail();

        await SignInAsync(User(UserStatus.Active, action), audit);

        var recorded = Assert.Single(audit.Recorded);
        Assert.Equal(SecurityEvent.SignInFailed, recorded.Event);
        Assert.Equal(expected, recorded.Reason);
        Assert.Equal([Target.User(UserId)], recorded.Targets);
    }

    [Fact]
    public async Task PendingVerification_RecordsAFailure()
    {
        var audit = new FakeAuthAuditTrail();

        await SignInAsync(User(UserStatus.PendingVerification), audit);

        Assert.Equal(OutcomeReason.PendingVerification, Assert.Single(audit.Recorded).Reason);
    }
}
