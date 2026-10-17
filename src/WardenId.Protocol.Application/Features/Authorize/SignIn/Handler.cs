using FluentResults;
using MediatR;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Domain;
using AuthenticateCommand = WardenId.Identity.Integration.Command.Features.Users.Authenticate.Command;
using AuthenticationFailure = WardenId.Identity.Integration.Command.Features.Users.Authenticate.AuthenticationFailure;
using FailureReason = WardenId.Identity.Integration.Command.Features.Users.Authenticate.FailureReason;
using RequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Protocol.Application.Features.Authorize.SignIn;

/// <summary>
/// Checks the credentials and whether the user may sign in now. Every refusal is audited as a failed sign-in
/// with its real reason; the user still sees one message for all of them. A success is audited by the session
/// start that follows (Sessions/Start), which knows the sid. Blocks come first; only an unblocked user gets the
/// steps, which aren't refusals and aren't audited: the code step when a factor is enabled, then the terms when
/// AcceptTos is pending, then enrollment when EnrollMfa is pending.
/// </summary>
public sealed class Handler(ISender sender, IAuthAuditTrail audit, TimeProvider timeProvider)
    : IRequestHandler<Command, Result<SignInOutcome>>
{
    // Most fundamental first: an unverified mailbox makes the other actions moot.
    private static readonly (RequiredAction Action, SignInBlockReason Reason)[] BlockPriority =
    [
        (RequiredAction.VerifyEmail, SignInBlockReason.VerifyEmail),
        (RequiredAction.ResetPassword, SignInBlockReason.ResetPassword)
    ];

    public async Task<Result<SignInOutcome>> Handle(Command request, CancellationToken cancellationToken)
    {
        var authResult = await sender.Send(
            new AuthenticateCommand(request.TenantId, request.EmailOrUsername, request.Password), cancellationToken);
        if (authResult.IsFailed)
        {
            var failure = AuthenticationFailure.Read(authResult.Errors);
            await RecordFailureAsync(
                request.TenantId,
                failure?.UserId,
                failure is { } tagged ? ToOutcomeReason(tagged.Reason) : OutcomeReason.InvalidCredentials);
            return Result.Fail(ProtocolErrors.InvalidCredentials());
        }

        var user = authResult.Value;
        if (user.Status is UserStatus.Disabled or UserStatus.Locked or UserStatus.SoftDeleted)
        {
            await RecordFailureAsync(request.TenantId, user.UserId, user.Status switch
            {
                UserStatus.Disabled => OutcomeReason.AccountDisabled,
                UserStatus.Locked => OutcomeReason.AccountLocked,
                _ => OutcomeReason.AccountDeleted
            });
            return Result.Fail(ProtocolErrors.InvalidCredentials());
        }

        SignInBlockReason? blocked = null;
        foreach (var (action, reason) in BlockPriority)
        {
            if (!user.PendingActions.Contains(action)) continue;
            blocked = reason;
            break;
        }

        if (blocked is null && user.Status == UserStatus.PendingVerification)
            blocked = SignInBlockReason.PendingVerification;

        if (blocked is { } blockReason)
        {
            await RecordFailureAsync(request.TenantId, user.UserId, ToOutcomeReason(blockReason));
            return Result.Ok(new SignInOutcome(user.UserId, blockReason, []));
        }

        List<SignInStep> steps = [];
        if (user.HasSecondFactor) steps.Add(SignInStep.SecondFactor);
        if (user.PendingActions.Contains(RequiredAction.AcceptTos)) steps.Add(SignInStep.AcceptTerms);
        if (user.PendingActions.Contains(RequiredAction.EnrollMfa)) steps.Add(SignInStep.EnrollTotp);

        return Result.Ok(new SignInOutcome(user.UserId, null, steps));
    }

    private Task RecordFailureAsync(Guid tenantId, Guid? userId, OutcomeReason reason) =>
        audit.RecordAsync(SignInAudit.Failed(tenantId, userId, reason, timeProvider.GetUtcNow()));

    private static OutcomeReason ToOutcomeReason(FailureReason reason) => SignInAudit.ReasonOf(reason);

    private static OutcomeReason ToOutcomeReason(SignInBlockReason reason) => reason switch
    {
        SignInBlockReason.VerifyEmail => OutcomeReason.EmailNotVerified,
        SignInBlockReason.ResetPassword => OutcomeReason.PasswordResetRequired,
        _ => OutcomeReason.PendingVerification
    };
}
