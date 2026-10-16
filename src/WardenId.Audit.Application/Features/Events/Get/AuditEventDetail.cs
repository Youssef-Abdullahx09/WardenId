using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Features.Events.Get;

/// <summary>
/// One audit event. The summary (event type, actor, targets, result) is always filled where known:
/// from the payload when it can be read, otherwise from the row's search columns. <see cref="Envelope"/>
/// is the whole decoded payload, and null with <see cref="PayloadAvailable"/> false once retention
/// purged it (or when it can't be read).
/// </summary>
/// <param name="Actor">Scope-qualified, e.g. "platform:adminuser:{id}".</param>
/// <param name="Targets">Each as "{type key}:{id}".</param>
public sealed record AuditEventDetail(
    Guid Id,
    AuditChannel Channel,
    Guid? TenantId,
    long? Sequence,
    DateTimeOffset OccurredAt,
    bool PayloadAvailable,
    string? EventType,
    string? Actor,
    IReadOnlyList<string> Targets,
    AuditResult? Result,
    AuditEnvelope? Envelope);

/// <summary>The decoded payload: every field of the audit envelope.</summary>
public sealed record AuditEnvelope(
    int SchemaVersion,
    AuditSeverity Severity,
    AuditEnvelopeActor Actor,
    IReadOnlyList<AuditEnvelopeTarget> Targets,
    AuditEnvelopeOutcome Outcome,
    AuditEnvelopeClient Client,
    AuditEnvelopeAuthContext AuthenticationContext,
    AuditEnvelopeCorrelation Correlation,
    IReadOnlyList<AuditEnvelopeChange> Changes);

/// <param name="Key">Scope-qualified form, e.g. "tenant:{tid}:adminuser:{id}".</param>
/// <param name="OnBehalfOf">The impersonated-by actor (the <c>act</c> claim), if any.</param>
public sealed record AuditEnvelopeActor(
    string Key,
    ActorType Type,
    string Id,
    bool IsPlatform,
    Guid? TenantId,
    AuditEnvelopeActor? OnBehalfOf);

/// <param name="Key">"{type key}:{id}".</param>
/// <param name="CreatedVia">How the target came to exist when no human created it, e.g. "dcr".</param>
public sealed record AuditEnvelopeTarget(string Key, string Type, string Id, string? CreatedVia);

/// <param name="Reason">Machine-readable reason code, e.g. "policy_denied".</param>
public sealed record AuditEnvelopeOutcome(AuditResult Status, string? Reason);

public sealed record AuditEnvelopeClient(string? IpAddress, string? UserAgent, string? Asn, string? GeoLocation);

public sealed record AuditEnvelopeAuthContext(
    string? Sid,
    string? Jti,
    string? Acr,
    IReadOnlyList<string> Amr,
    DateTimeOffset? AuthTime,
    string? Protocol);

public sealed record AuditEnvelopeCorrelation(string? RequestId, string? TraceId, Guid? TransactionId);

/// <summary>One property's before/after. Old is null on create.</summary>
public sealed record AuditEnvelopeChange(string PropertyName, AuditEnvelopeValue? Old, AuditEnvelopeValue? New);

/// <summary>
/// A captured value. Plain values carry <see cref="Value"/>; redacted ones never do, only the
/// SHA-256 <see cref="Hash"/> and, when one was kept, a short <see cref="Prefix"/>.
/// </summary>
public sealed record AuditEnvelopeValue(bool Redacted, string? Value, string? Hash, string? Prefix);
