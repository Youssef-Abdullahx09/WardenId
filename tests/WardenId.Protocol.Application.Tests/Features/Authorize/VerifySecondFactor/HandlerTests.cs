using Microsoft.Extensions.Time.Testing;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using WardenId.Protocol.Application.Features.Authorize.VerifySecondFactor;
using Command = WardenId.Protocol.Application.Features.Authorize.VerifySecondFactor.Command;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;
using IdentityCommand = WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor.Command;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Protocol.Application.Tests.Features.Authorize.VerifySecondFactor;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-16T08:00:00Z");

    private static Task<Result<SecondFactorPassed>> VerifyAsync(StubSender sender, FakeAuthAuditTrail audit) =>
        new Handler(sender, audit, new FakeTimeProvider(Now))
            .Handle(new Command(TenantId, UserId, "123456"), CancellationToken.None);

    private static StubSender Answers(Result<SecondFactorVerified> result) =>
        new StubSender().Returns<IdentityCommand, Result<SecondFactorVerified>>(result);

    private static Result<SecondFactorVerified> Failed(SecondFactorFailureReason reason) =>
        Result.Fail<SecondFactorVerified>(SecondFactorFailure.Tag(new Error("Invalid code."), reason));

    [Fact]
    public async Task Totp_Passes_WithOtpAndMfaAmr_AndRecordsNothing()
    {
        var sender = Answers(Result.Ok(new SecondFactorVerified(SecondFactorMethod.Totp, 10)));
        var audit = new FakeAuthAuditTrail();

        var result = await VerifyAsync(sender, audit);

        Assert.Equal(["pwd", "otp", "mfa"], result.Value.Amr);
        Assert.Empty(audit.Recorded);
        var sent = Assert.IsType<IdentityCommand>(Assert.Single(sender.Sent));
        Assert.Equal((TenantId, UserId, "123456"), (sent.TenantId, sent.UserId, sent.Code));
    }

    [Fact]
    public async Task RecoveryCode_Passes_WithMfaButNotOtpAmr()
    {
        var result = await VerifyAsync(
            Answers(Result.Ok(new SecondFactorVerified(SecondFactorMethod.RecoveryCode, 9))), new FakeAuthAuditTrail());

        Assert.Equal(["pwd", "mfa"], result.Value.Amr);
    }

    [Theory]
    [InlineData(SecondFactorFailureReason.InvalidCode, OutcomeReason.InvalidSecondFactor)]
    [InlineData(SecondFactorFailureReason.LockedOut, OutcomeReason.AccountLockedOut)]
    [InlineData(SecondFactorFailureReason.NotEnrolled, OutcomeReason.InvalidCredentials)]
    public async Task Failure_IsAuditedWithItsReason_AndAnsweredGenerically(
        SecondFactorFailureReason failure, OutcomeReason expected)
    {
        var audit = new FakeAuthAuditTrail();

        var result = await VerifyAsync(Answers(Failed(failure)), audit);

        Assert.Equal("That code didn't work. Please try again.", result.Errors.Single().Message);
        var recorded = Assert.Single(audit.Recorded);
        Assert.Equal(SecurityEvent.SignInFailed, recorded.Event);
        Assert.Equal(expected, recorded.Reason);
        Assert.Equal(TenantId, recorded.TenantId);
        Assert.Equal(Now, recorded.OccurredAt);
        Assert.Equal(Actor.User(UserId), recorded.Actor);
        Assert.Equal([Target.User(UserId)], recorded.Targets);
    }

    [Fact]
    public async Task UntaggedFailure_IsAuditedAsInvalidCredentials()
    {
        var audit = new FakeAuthAuditTrail();

        var result = await VerifyAsync(Answers(Result.Fail<SecondFactorVerified>("boom")), audit);

        Assert.True(result.IsFailed);
        Assert.Equal(OutcomeReason.InvalidCredentials, Assert.Single(audit.Recorded).Reason);
    }

    [Theory]
    [InlineData(UserStatus.Locked, OutcomeReason.AccountLocked)]
    [InlineData(UserStatus.Disabled, OutcomeReason.AccountDisabled)]
    public async Task InactiveAccount_IsAuditedWithTheActualStatus(UserStatus status, OutcomeReason expected)
    {
        var sender = Answers(Failed(SecondFactorFailureReason.AccountInactive))
            .Returns<GetForSignInQuery, Result<SignInUser>>(
                Result.Ok(new SignInUser(UserId, "alice@acme.test", "alice", status, [])));
        var audit = new FakeAuthAuditTrail();

        await VerifyAsync(sender, audit);

        Assert.Equal(expected, Assert.Single(audit.Recorded).Reason);
    }

    [Fact]
    public async Task InactiveAccount_ThatIsGone_IsAuditedAsDeleted()
    {
        var sender = Answers(Failed(SecondFactorFailureReason.AccountInactive))
            .Returns<GetForSignInQuery, Result<SignInUser>>(Result.Fail<SignInUser>("Not found."));
        var audit = new FakeAuthAuditTrail();

        await VerifyAsync(sender, audit);

        Assert.Equal(OutcomeReason.AccountDeleted, Assert.Single(audit.Recorded).Reason);
    }
}
