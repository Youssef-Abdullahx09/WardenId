using WardenId.Identity.Domain;

namespace WardenId.Identity.Application;

/// <summary>
/// The global identity policy (IdentityPolicy config section). A tenant can override any of these
/// except <see cref="CurrentPasswordAlgorithmVersion"/> (see IdentityTenantPolicy); the values here
/// apply wherever it doesn't. Read them through <see cref="IdentityPolicyResolver"/>, never directly.
/// </summary>
public sealed class IdentityPolicyOptions
{
    public int PasswordMinLength { get; init; } = 8;
    public bool PasswordRequireUppercase { get; init; } = true;
    public bool PasswordRequireDigit { get; init; } = true;
    public bool PasswordRequireSymbol { get; init; }
    public bool PasswordBreachCheckEnabled { get; init; } = true;
    public int PasswordHistoryCount { get; init; } = 5;
    public int CurrentPasswordAlgorithmVersion { get; init; } = 1;
    public int MaxFailedLoginAttempts { get; init; } = 5;
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);
    public MfaPolicyMode MfaPolicyMode { get; init; } = MfaPolicyMode.Optional;

    /// <summary>Whether anyone may sign up through POST tenants/{tenantId}/auth/register. Admin user creation is unaffected.</summary>
    public bool SelfRegistrationEnabled { get; init; } = true;
}
