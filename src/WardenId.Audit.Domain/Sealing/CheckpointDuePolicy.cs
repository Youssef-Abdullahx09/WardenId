namespace WardenId.Audit.Domain.Sealing;

/// <summary>
/// Segments-or-time checkpoint trigger — "whichever is looser" per
/// docs/audit/audit-module-spec.md §6, i.e. the LONGER of the two intervals, since signing
/// is meant to be rare (keeps signing-key access infrequent). Opposite polarity from
/// <see cref="SegmentSealPolicy"/>, which fires on whichever condition is EARLIER.
/// </summary>
public static class CheckpointDuePolicy
{
    public static bool IsDue(
        int segmentsSinceLastCheckpoint, TimeSpan sinceLastCheckpoint, int maxSegments, TimeSpan maxInterval)
    {
        if (segmentsSinceLastCheckpoint <= 0) return false;
        return segmentsSinceLastCheckpoint >= maxSegments && sinceLastCheckpoint >= maxInterval;
    }
}
