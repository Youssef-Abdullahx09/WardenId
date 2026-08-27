namespace WardenId.Audit.Infrastructure.Exporting;

/// <summary>
/// CAP message body for one exported segment (spec §5's "Exporter → CAP publish sealed
/// segments to external sinks"; payload shape per docs/audit/OPEN-DECISIONS.md #7 — full
/// events, not just the header, so an external sink is a genuine archive/witness tier). Carries
/// the exact canonical Payload bytes already persisted at insert time (I2) — never re-derived.
/// </summary>
public sealed class AuditSegmentExportedMessage
{
    public Guid SegmentId { get; init; }
    public int Channel { get; init; }
    public Guid? TenantId { get; init; }
    public long SequenceStart { get; init; }
    public long SequenceEnd { get; init; }
    public byte[] MerkleRoot { get; init; } = [];
    public byte[]? PreviousHeaderHash { get; init; }
    public byte[] HeaderHash { get; init; } = [];
    public DateTimeOffset SealedAt { get; init; }
    public IReadOnlyList<AuditExportedEvent> Events { get; init; } = [];
}

public sealed class AuditExportedEvent
{
    public Guid Id { get; init; }
    public long Sequence { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public byte[] Payload { get; init; } = [];
    public byte[] LeafHash { get; init; } = [];
}
