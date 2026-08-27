namespace WardenId.SharedKernel.Persistence.Audit;

/// <summary>
/// The persisted shape of one audit row — audit.events. Deliberately a bare POCO, not an
/// Entity/AggregateRoot: no CreatedById/LastModificationById/IsDeleted (spec §11 — the actor
/// is already inside Payload, and audit rows are never soft-deleted or updated except by the
/// retention path nulling Payload). Deliberately primitive-typed rather than using
/// WardenId.Audit.Domain's rich types (AuditChannel, etc.) — this project has zero dependency
/// on Audit.Domain, so every module can map this same type without inverting the dependency
/// graph. Sequence stays null until the sealer assigns a dense value (I3); Payload is nulled
/// by retention while LeafHash survives so roots stay recomputable (I6).
/// </summary>
public sealed class AuditEventRecord
{
    public Guid Id { get; set; }
    public long? Sequence { get; set; }
    public string Channel { get; set; }
    public Guid? TenantId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public byte[]? Payload { get; set; }
    public byte[] LeafHash { get; set; }
}
