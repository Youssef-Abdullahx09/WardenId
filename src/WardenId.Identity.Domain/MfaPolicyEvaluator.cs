using FluentResults;

namespace WardenId.Identity.Domain;

public static class MfaPolicyEvaluator
{
    public static bool IsMfaRequired(MfaPolicy policy, bool userIsAdmin) =>
        policy.Mode switch
        {
            MfaPolicyMode.Required => true,
            MfaPolicyMode.RequiredForAdmins => userIsAdmin,
            _ => false
        };

    public static Result CanRevokeFactor(MfaPolicy policy, User user, MfaFactor factor, bool userIsAdmin)
    {
        if (!IsMfaRequired(policy, userIsAdmin)) return Result.Ok();

        var remainingEnabledFactors = user.MfaFactors.Count(f => f.Enabled && f.Id != factor.Id);

        return remainingEnabledFactors == 0
            ? Result.Fail(UserErrors.CannotRevokeLastRequiredFactor())
            : Result.Ok();
    }
}
