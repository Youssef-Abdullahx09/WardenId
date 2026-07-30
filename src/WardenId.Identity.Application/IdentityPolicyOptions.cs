using WardenId.Identity.Domain;

namespace WardenId.Identity.Application;

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
}
