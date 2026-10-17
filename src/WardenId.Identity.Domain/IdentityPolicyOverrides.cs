using FluentResults;

namespace WardenId.Identity.Domain;

/// <summary>
/// A tenant's overrides of the global identity policy. Every field is optional: null means the
/// tenant uses the global value (the IdentityPolicy config section). A tenant may set a value only
/// within <see cref="IdentityPolicyBounds"/>; the global configuration itself is not bound by them.
/// </summary>
public sealed record IdentityPolicyOverrides(
    int? PasswordMinLength,
    bool? PasswordRequireUppercase,
    bool? PasswordRequireDigit,
    bool? PasswordRequireSymbol,
    bool? PasswordBreachCheckEnabled,
    int? PasswordHistoryCount,
    int? MaxFailedLoginAttempts,
    TimeSpan? LockoutDuration,
    MfaPolicyMode? MfaMode,
    bool? SelfRegistrationEnabled)
{
    public static readonly IdentityPolicyOverrides None = new(null, null, null, null, null, null, null, null, null, null);

    public bool HasAny => this != None;

    public Result Validate()
    {
        if (PasswordMinLength is < IdentityPolicyBounds.MinPasswordMinLength or > IdentityPolicyBounds.MaxPasswordMinLength)
            return Result.Fail(IdentityPolicyErrors.PasswordMinLengthOutOfRange());

        if (PasswordHistoryCount is < IdentityPolicyBounds.MinPasswordHistoryCount or > IdentityPolicyBounds.MaxPasswordHistoryCount)
            return Result.Fail(IdentityPolicyErrors.PasswordHistoryCountOutOfRange());

        if (MaxFailedLoginAttempts is < IdentityPolicyBounds.MinMaxFailedLoginAttempts or > IdentityPolicyBounds.MaxMaxFailedLoginAttempts)
            return Result.Fail(IdentityPolicyErrors.MaxFailedLoginAttemptsOutOfRange());

        if (LockoutDuration is { } duration &&
            (duration < IdentityPolicyBounds.MinLockoutDuration || duration > IdentityPolicyBounds.MaxLockoutDuration))
            return Result.Fail(IdentityPolicyErrors.LockoutDurationOutOfRange());

        if (MfaMode is { } mode && !Enum.IsDefined(mode))
            return Result.Fail(IdentityPolicyErrors.MfaModeInvalid());

        return Result.Ok();
    }
}

/// <summary>What a tenant may set. The global configuration is not held to these.</summary>
public static class IdentityPolicyBounds
{
    /// <summary>NIST SP 800-63B's floor for user-chosen passwords: a tenant may raise it, never go under it.</summary>
    public const int MinPasswordMinLength = 8;

    /// <summary>Room for long passphrases; a higher minimum only locks users out.</summary>
    public const int MaxPasswordMinLength = 128;

    /// <summary>0 turns the reuse check off.</summary>
    public const int MinPasswordHistoryCount = 0;

    /// <summary>The usual compliance ceiling (Active Directory's maximum); each entry costs a hash check on every password change.</summary>
    public const int MaxPasswordHistoryCount = 24;

    /// <summary>Lock on the first failure at the strictest.</summary>
    public const int MinMaxFailedLoginAttempts = 1;

    /// <summary>NIST SP 800-63B: at most 100 consecutive failures before throttling.</summary>
    public const int MaxMaxFailedLoginAttempts = 100;

    /// <summary>Anything shorter barely slows a guessing attack.</summary>
    public static readonly TimeSpan MinLockoutDuration = TimeSpan.FromMinutes(1);

    /// <summary>Anyone who knows a username can trigger a lockout, so a longer one is a denial-of-service lever. Admins can unlock sooner.</summary>
    public static readonly TimeSpan MaxLockoutDuration = TimeSpan.FromHours(24);
}
