namespace WardenId.Audit.Infrastructure.Sealing;

/// <summary>
/// Bound from ConfigurationKeys.Sealer.Section. Defaults match
/// docs/audit/OPEN-DECISIONS.md #4 — one global default for now (not yet differentiated per
/// channel), exposed via config so tuning it is a config change, not a code change.
/// </summary>
public sealed class AuditSealerOptions
{
    public int PollIntervalSeconds { get; init; } = 30;
    public int MaxSegmentSize { get; init; } = 10_000;
    public int MaxSegmentAgeSeconds { get; init; } = 300;
    public int MaxSegmentsPerCheckpoint { get; init; } = 10;
    public int MaxCheckpointIntervalSeconds { get; init; } = 3600;

    public TimeSpan MaxSegmentAge => TimeSpan.FromSeconds(MaxSegmentAgeSeconds);
    public TimeSpan MaxCheckpointInterval => TimeSpan.FromSeconds(MaxCheckpointIntervalSeconds);
    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);
}
