namespace WardenId.Audit.Domain.Sealing;

/// <summary>
/// Size-or-time seal trigger — see docs/audit/audit-module-spec.md §6. Size alone means a
/// quiet channel/tenant never seals (pending rows sit unsealed and unqueryable-as-proven
/// forever); time alone means a busy one builds unbounded segments. Pure decision logic,
/// no I/O, so the sealer's Infrastructure loop stays a thin orchestrator around this.
/// </summary>
public static class SegmentSealPolicy
{
    public static bool ShouldSeal(int pendingCount, TimeSpan sinceOldestPending, int maxSegmentSize, TimeSpan maxSegmentAge)
    {
        if (pendingCount <= 0) return false;
        return pendingCount >= maxSegmentSize || sinceOldestPending >= maxSegmentAge;
    }
}
