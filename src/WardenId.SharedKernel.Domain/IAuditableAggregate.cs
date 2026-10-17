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
    private static readonly IReadOnlySet<string> NoNavigations = new HashSet<string>();

    string AuditTargetTypeKey { get; }
    AuditableChannel AuditChannel { get; }

    /// <summary>
    /// Property names whose value must never appear as plain text in an audit diff — captured as a hash+prefix instead.
    /// A child property listed in <see cref="AuditedNavigationNames"/> is named "{Navigation}.{Property}".
    /// </summary>
    IReadOnlySet<string> AuditSensitivePropertyNames { get; }

    /// <summary>
    /// Navigations (child entities, owned collections, owned references) whose added, changed and removed
    /// children are diffed into the aggregate's own event, as "{Navigation}[{child key}].{Property}" (a
    /// reference: "{Navigation}.{Property}"). A change to only these children still produces an update
    /// event. Opt-in, so a child holding secrets is never diffed by accident. Default: none.
    /// </summary>
    IReadOnlySet<string> AuditedNavigationNames => NoNavigations;

    /// <summary>The audit target id; null means the entity's <see cref="IEntity.Id"/>. Default: null.</summary>
    string? AuditTargetId => null;
}
