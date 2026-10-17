using FluentResults;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application;

/// <summary>The identity policy in force for one tenant: its overrides laid over the global options.</summary>
/// <param name="CurrentPasswordAlgorithmVersion">Always the global value: hashing isn't a tenant setting.</param>
/// <param name="TermsUrl">The terms of service link, or null when neither the tenant nor the global options set one.</param>
/// <param name="Overrides">The tenant's own values (all null when it has none).</param>
public sealed record EffectiveIdentityPolicy(
    PasswordPolicy Password,
    int MaxFailedLoginAttempts,
    TimeSpan LockoutDuration,
    MfaPolicy Mfa,
    bool SelfRegistrationEnabled,
    int CurrentPasswordAlgorithmVersion,
    string? TermsUrl,
    IdentityPolicyOverrides Overrides)
{
    public static Result<EffectiveIdentityPolicy> Merge(IdentityPolicyOptions global, IdentityPolicyOverrides? tenant)
    {
        var overrides = tenant ?? IdentityPolicyOverrides.None;

        var password = PasswordPolicy.Create(
            overrides.PasswordMinLength ?? global.PasswordMinLength,
            overrides.PasswordRequireUppercase ?? global.PasswordRequireUppercase,
            overrides.PasswordRequireDigit ?? global.PasswordRequireDigit,
            overrides.PasswordRequireSymbol ?? global.PasswordRequireSymbol,
            overrides.PasswordBreachCheckEnabled ?? global.PasswordBreachCheckEnabled,
            overrides.PasswordHistoryCount ?? global.PasswordHistoryCount);
        if (password.IsFailed) return Result.Fail(password.Errors);

        return Result.Ok(new EffectiveIdentityPolicy(
            password.Value,
            overrides.MaxFailedLoginAttempts ?? global.MaxFailedLoginAttempts,
            overrides.LockoutDuration ?? global.LockoutDuration,
            MfaPolicy.Create(overrides.MfaMode ?? global.MfaPolicyMode),
            overrides.SelfRegistrationEnabled ?? global.SelfRegistrationEnabled,
            global.CurrentPasswordAlgorithmVersion,
            overrides.TermsUrl ?? global.TermsUrl,
            overrides));
    }
}
