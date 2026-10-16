using FluentResults;

namespace WardenId.Identity.Domain;

public static class MfaPolicyEvaluator
{
    /// <summary>
    /// Whether the policy requires MFA of a user. WardenId has no roles, so it can't tell which
    /// users are admins: <see cref="MfaPolicyMode.RequiredForAdmins"/> fails closed and requires
    /// MFA of every user, so an admin's last factor is never removable.
    /// </summary>
    public static bool IsMfaRequired(MfaPolicy policy) =>
        policy.Mode is MfaPolicyMode.Required or MfaPolicyMode.RequiredForAdmins;

    public static Result CanRevokeFactor(MfaPolicy policy, User user, MfaFactor factor)
    {
        if (!IsMfaRequired(policy)) return Result.Ok();

        var remainingEnabledFactors = user.MfaFactors.Count(f => f.Enabled && f.Id != factor.Id);

        return remainingEnabledFactors == 0
            ? Result.Fail(UserErrors.CannotRevokeLastRequiredFactor())
            : Result.Ok();
    }
}
