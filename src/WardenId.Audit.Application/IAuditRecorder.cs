using WardenId.Audit.Domain;

namespace WardenId.Audit.Application;

/// <summary>
/// Direct-append write path for channels with no ambient transaction (authn.*, token.ops —
/// spec §5). Implementations must fail open (I10): a write failure here must never surface
/// to the caller. tenantId is the ambient tenant scope of the operation (see
/// AuditAmbientContext), passed explicitly rather than read off the envelope, since it isn't
/// part of the envelope itself.
/// </summary>
public interface IAuditRecorder
{
    Task RecordAsync(AuditEvent auditEvent, Guid? tenantId, CancellationToken cancellationToken = default);
}
