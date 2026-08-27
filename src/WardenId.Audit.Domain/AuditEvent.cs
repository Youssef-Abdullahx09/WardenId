using FluentResults;

namespace WardenId.Audit.Domain;

/// <summary>
/// One envelope for every audited action — see docs/audit/audit-module-spec.md §3.2. There
/// is no untyped bag anywhere here; a new field means a typed property and a SchemaVersion
/// bump, never a Dictionary&lt;string, object&gt;.
/// </summary>
public sealed record AuditEvent
{
    public Guid Id { get; }
    public AuditChannel Channel { get; }
    public AuditEventType EventType { get; }
    public int SchemaVersion { get; }
    public DateTimeOffset OccurredAt { get; }
    public AuditSeverity Severity { get; }
    public Actor Actor { get; }
    public IReadOnlyList<AuditTarget> Targets { get; }
    public AuditOutcome Outcome { get; }
    public ClientContext Client { get; }
    public AuthContext AuthenticationContext { get; }
    public Correlation Correlation { get; }
    public IReadOnlyList<PropertyChange> Changes { get; }

    private AuditEvent(
        Guid id, AuditChannel channel, AuditEventType eventType, int schemaVersion, DateTimeOffset occurredAt,
        AuditSeverity severity, Actor actor, IReadOnlyList<AuditTarget> targets, AuditOutcome outcome,
        ClientContext client, AuthContext authenticationContext, Correlation correlation,
        IReadOnlyList<PropertyChange> changes)
    {
        Id = id;
        Channel = channel;
        EventType = eventType;
        SchemaVersion = schemaVersion;
        OccurredAt = occurredAt;
        Severity = severity;
        Actor = actor;
        Targets = targets;
        Outcome = outcome;
        Client = client;
        AuthenticationContext = authenticationContext;
        Correlation = correlation;
        Changes = changes;
    }

    public static Result<AuditEvent> Create(
        Guid id,
        AuditChannel channel,
        AuditEventType eventType,
        int schemaVersion,
        DateTimeOffset occurredAt,
        AuditSeverity severity,
        Actor actor,
        AuditOutcome outcome,
        ClientContext client,
        AuthContext authenticationContext,
        Correlation correlation,
        IReadOnlyList<AuditTarget>? targets = null,
        IReadOnlyList<PropertyChange>? changes = null)
    {
        if (schemaVersion < 1)
            return Result.Fail(AuditErrors.SchemaVersionMustBePositive());

        return Result.Ok(new AuditEvent(
            id, channel, eventType, schemaVersion, occurredAt, severity, actor,
            targets ?? [], outcome, client, authenticationContext, correlation, changes ?? []));
    }
}
