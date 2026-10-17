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

/// <param name="Enabled">Whether POST tenants/{tenantId}/auth/register accepts sign-ups. Admin user creation is unaffected.</param>
public sealed record SelfRegistrationSettings(bool Enabled);

/// <param name="Url">The terms of service link shown at the accept-terms sign-in step, or null when none is set.</param>
public sealed record TermsSettings(string? Url);

/// <summary>The effective identity policy for a tenant: the tenant's overrides over the global values.</summary>
/// <param name="Scope">"tenant" when at least one value is the tenant's own, else "global" (all from process configuration).</param>
/// <param name="Overrides">
/// The fields the tenant overrides, as JSON paths into this object (e.g. "password.minLength", "mfa.mode", "terms.url");
/// every other value is the global one. Empty when <paramref name="Scope"/> is "global".
/// </param>
public sealed record IdentityPolicy(
    PasswordSettings Password,
    LockoutSettings Lockout,
    MfaSettings Mfa,
    SelfRegistrationSettings SelfRegistration,
    TermsSettings Terms,
    string Scope,
    IReadOnlyList<string> Overrides);
