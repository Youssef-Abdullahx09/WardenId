using WardenId.Audit.Domain;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Audit.Infrastructure;

/// <summary>
/// The one place an <see cref="AuditEvent"/> becomes an audit.events row, shared by both write
/// paths (<see cref="AuditRecorder"/>, <see cref="AuditCaptureInterceptor"/>). Serializes the
/// envelope once and hashes those exact bytes (I1/I2); the search columns are read from the same
/// AuditEvent, never from the payload, and are not part of what gets hashed.
/// </summary>
public static class AuditEventRecordFactory
{
    public static AuditEventRecord Create(AuditEvent auditEvent, Guid? tenantId)
    {
        var payload = CanonicalAuditEventSerializer.Serialize(auditEvent);

        return new AuditEventRecord
        {
            Id = auditEvent.Id,
            Channel = auditEvent.Channel.ToString(),
            TenantId = tenantId,
            OccurredAt = auditEvent.OccurredAt,
            Payload = payload,
            LeafHash = MerkleHash.Leaf(payload),
            EventType = auditEvent.EventType.Key,
            Actor = auditEvent.Actor.ToSerializedForm(),
            ActorId = auditEvent.Actor.Id,
            Targets = auditEvent.Targets.Select(t => t.ToSerializedForm()).ToArray(),
            Outcome = auditEvent.Outcome.Status.ToString()
        };
    }
}
