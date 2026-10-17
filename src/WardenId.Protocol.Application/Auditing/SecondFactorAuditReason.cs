using FluentResults;
using MediatR;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Protocol.Application.Auditing;

/// <summary>Turns Identity's <see cref="SecondFactorFailure"/> tag into the audit outcome reason.</summary>
internal static class SecondFactorAuditReason
{
    /// <summary>
    /// The reason a code check failed, or null when Identity checked no code: an untagged failure (a policy refusal,
    /// say) or a user with no factor.
    /// </summary>
    public static async Task<OutcomeReason?> OfAsync(
        ISender sender, Guid tenantId, Guid userId, IEnumerable<IError> errors, CancellationToken cancellationToken) =>
        SecondFactorFailure.Read(errors) switch
        {
            SecondFactorFailureReason.InvalidCode => OutcomeReason.InvalidSecondFactor,
            SecondFactorFailureReason.LockedOut => OutcomeReason.AccountLockedOut,
            SecondFactorFailureReason.AccountInactive => await InactiveReasonAsync(sender, tenantId, userId, cancellationToken),
            _ => null
        };

    /// <summary>Identity says only "locked, disabled or deleted"; the audit row names which.</summary>
    private static async Task<OutcomeReason> InactiveReasonAsync(
        ISender sender, Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await sender.Send(new GetForSignInQuery(tenantId, userId), cancellationToken);
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
