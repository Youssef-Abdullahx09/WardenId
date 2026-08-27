using WardenId.Audit.Domain;

namespace WardenId.Audit.Infrastructure.Retention;

/// <summary>
/// Bound from ConfigurationKeys.Retention.Section. Defaults match
/// docs/audit/OPEN-DECISIONS.md #1 (user-confirmed numbers, not the spec's qualitative
/// long/short) — exposed via config, same posture as AuditSealerOptions.
/// </summary>
public sealed class AuditRetentionOptions
{
    public int PollIntervalSeconds { get; init; } = 3600;

    public int AuthnInteractiveDays { get; init; } = 400;
    public int AuthnSilentDays { get; init; } = 30;
    public int AuthnMachineDays { get; init; } = 14;
    public int AdminDirectoryDays { get; init; } = 2555;
    public int AdminConfigDays { get; init; } = 2555;
    public int TokenOpsDays { get; init; } = 1;
    public int PlatformDays { get; init; } = 2555;

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);

    public TimeSpan RetentionWindowFor(AuditChannel channel) => TimeSpan.FromDays(channel switch
    {
        AuditChannel.AuthnInteractive => AuthnInteractiveDays,
        AuditChannel.AuthnSilent => AuthnSilentDays,
        AuditChannel.AuthnMachine => AuthnMachineDays,
        AuditChannel.AdminDirectory => AdminDirectoryDays,
        AuditChannel.AdminConfig => AdminConfigDays,
        AuditChannel.TokenOps => TokenOpsDays,
        AuditChannel.Platform => PlatformDays,
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unmapped audit channel.")
    });
}
