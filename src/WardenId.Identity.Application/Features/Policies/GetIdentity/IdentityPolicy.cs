using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Policies.GetIdentity;

public sealed record PasswordSettings(
    int MinLength,
    bool RequireUppercase,
    bool RequireDigit,
    bool RequireSymbol,
    bool BreachCheckEnabled,
    int HistoryCount);

/// <param name="LockoutDuration">ISO 8601 duration, e.g. "PT15M".</param>
public sealed record LockoutSettings(int MaxFailedAttempts, string LockoutDuration);

public sealed record MfaSettings(MfaPolicyMode Mode);

/// <param name="Scope">Where the values come from: "global" (process configuration) until per-tenant policies exist.</param>
public sealed record IdentityPolicy(
    PasswordSettings Password,
    LockoutSettings Lockout,
    MfaSettings Mfa,
    string Scope);
