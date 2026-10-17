using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users;

internal static class MfaRequirement
{
    /// <summary>
    /// Whether the user has to enroll an MFA factor: they have the EnrollMfa required action, or the tenant's MFA
    /// policy requires MFA and they have no enabled factor.
    /// </summary>
    public static bool MustEnroll(User user, MfaPolicy policy) =>
        user.PendingActions.Contains(RequiredAction.EnrollMfa)
        || (MfaPolicyEvaluator.IsMfaRequired(policy) && !user.HasEnabledMfaFactor);

    /// <summary>
    /// Whether the user may start a TOTP enrollment with no code of a current factor: they have to enroll
    /// (<see cref="MustEnroll"/>), or they have no enabled factor and the policy isn't Off (optional self-enrollment
    /// from the account page). A user who already has an enabled factor and needn't enroll must prove they hold it.
    /// </summary>
    public static bool MayEnrollWithoutCode(User user, MfaPolicy policy) =>
        MustEnroll(user, policy)
        || (!user.HasEnabledMfaFactor && policy.Mode != MfaPolicyMode.Off);
}
