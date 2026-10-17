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
}
