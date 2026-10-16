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
///
/// EventType, Actor, ActorId, Targets and Outcome are search columns: copies of envelope fields,
/// set at write time from the same AuditEvent the payload is serialized from, so events can be
/// filtered without decoding payloads. They are not hashed and not part of the payload, so they
/// play no part in sealing or verification. Null on rows whose payload retention had already
/// purged when the columns were added (nothing left to read them from), and on unreadable
/// payloads.
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

    /// <summary>Taxonomy key, e.g. "admin.entity.update".</summary>
    public string? EventType { get; set; }

    /// <summary>Scope-qualified actor, e.g. "platform:adminuser:{id}" / "tenant:{tid}:oauthclient:{id}".</summary>
    public string? Actor { get; set; }

    /// <summary>The actor's bare id, for searching by id across scopes and actor types.</summary>
    public string? ActorId { get; set; }

    /// <summary>Every target as "{type key}:{id}".</summary>
    public string[]? Targets { get; set; }

    /// <summary>Outcome status name, "Success" or "Failure".</summary>
    public string? Outcome { get; set; }
}
