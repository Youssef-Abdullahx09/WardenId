namespace WardenId.Audit.Domain.Retention;

/// <summary>
/// Hash-preserving purge trigger — see docs/audit/audit-module-spec.md §4 (I6) and
/// docs/audit/OPEN-DECISIONS.md #6. A row is only purge-eligible once the segment covering it
/// has actually been exported: purging ahead of export would let a purely age-based sweep null
/// the only copy of a payload before any external archive/witness tier ever saw it. Pure
/// decision logic, no I/O, same posture as SegmentSealPolicy/CheckpointDuePolicy.
/// </summary>
public static class RetentionPolicy
{
    public static bool IsPurgeable(DateTimeOffset occurredAt, DateTimeOffset now, TimeSpan retentionWindow, bool isExported)
    {
        return isExported && now - occurredAt >= retentionWindow;
    }
}
