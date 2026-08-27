namespace WardenId.SharedKernel.Domain;

/// <summary>
/// Opt-in marker for aggregates whose Created/Modified changes should be captured
/// automatically as admin.* audit events (spec I8). AuditTargetTypeKey is a plain string,
/// not the Audit module's own AuditTargetType enum, for the same reason AuditableChannel
/// exists here rather than referencing AuditChannel directly: SharedKernel.Domain must have
/// zero dependency on any feature module. The audit interceptor (which does depend on
/// Audit.Domain) resolves this key via AuditTargetType.FromKey and fails loudly if it isn't
/// registered there — matching how the Audit module's own canonical deserializer already
/// treats an unrecognized key as corruption, not a silently-skipped write.
/// </summary>
public interface IAuditableAggregate
{
    string AuditTargetTypeKey { get; }
    AuditableChannel AuditChannel { get; }

    /// <summary>Property names whose value must never appear as plain text in an audit diff — captured as a hash+prefix instead.</summary>
    IReadOnlySet<string> AuditSensitivePropertyNames { get; }
}
