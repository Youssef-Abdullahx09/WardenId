using FluentResults;
using MediatR;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Domain;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;
using IdentityVerifySecondFactorCommand = WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor.Command;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Protocol.Application.Features.Authorize.VerifySecondFactor;

/// <summary>
/// Checks the code with Identity (which counts wrong codes towards lockout). Every failure is audited as a failed
/// sign-in with its real reason; the user always gets the same message.
/// </summary>
public sealed class Handler(ISender sender, IAuthAuditTrail audit, TimeProvider timeProvider)
    : IRequestHandler<Command, Result<SecondFactorPassed>>
{
    public async Task<Result<SecondFactorPassed>> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new IdentityVerifySecondFactorCommand(request.TenantId, request.UserId, request.Code), cancellationToken);
        if (result.IsSuccess)
            return Result.Ok(new SecondFactorPassed(result.Value.Method == SecondFactorMethod.RecoveryCode
                ? AuthenticationMethods.PasswordAndRecoveryCode
                : AuthenticationMethods.PasswordAndTotp));

        var reason = SecondFactorFailure.Read(result.Errors) switch
        {
            SecondFactorFailureReason.InvalidCode => OutcomeReason.InvalidSecondFactor,
            SecondFactorFailureReason.LockedOut => OutcomeReason.AccountLockedOut,
            SecondFactorFailureReason.AccountInactive => await InactiveReasonAsync(request, cancellationToken),
            _ => OutcomeReason.InvalidCredentials
        };
        await audit.RecordAsync(SignInAudit.Failed(request.TenantId, request.UserId, reason, timeProvider.GetUtcNow()));
        return Result.Fail(ProtocolErrors.InvalidSecondFactor());
    }

    /// <summary>Identity says only "locked, disabled or deleted"; the audit row names which.</summary>
    private async Task<OutcomeReason> InactiveReasonAsync(Command request, CancellationToken cancellationToken)
    {
        var user = await sender.Send(new GetForSignInQuery(request.TenantId, request.UserId), cancellationToken);
        if (user.IsFailed) return OutcomeReason.AccountDeleted;

        return user.Value.Status switch
        {
            UserStatus.Locked => OutcomeReason.AccountLocked,
            UserStatus.Disabled => OutcomeReason.AccountDisabled,
            UserStatus.SoftDeleted => OutcomeReason.AccountDeleted,
            _ => OutcomeReason.InvalidCredentials
        };
    }
}
