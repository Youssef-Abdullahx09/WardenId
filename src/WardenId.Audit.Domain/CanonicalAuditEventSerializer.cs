using System.Text.Json;
using FluentResults;

namespace WardenId.Audit.Domain;

/// <summary>
/// The only place an AuditEvent turns into bytes for hashing/storage, and the only place
/// those bytes turn back into an AuditEvent. Deserialize trusts its input (the bytes it is
/// given always originated from Serialize) — a shape violation here means the payload is
/// corrupt or tampered, so it throws rather than returning a Result. See I1/I2.
/// </summary>
public static class CanonicalAuditEventSerializer
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static byte[] Serialize(AuditEvent auditEvent)
        => JsonSerializer.SerializeToUtf8Bytes(ToCanonical(auditEvent), Options);

    public static AuditEvent Deserialize(byte[] canonicalBytes)
    {
        var canonical = JsonSerializer.Deserialize<CanonicalAuditEvent>(canonicalBytes, Options)
            ?? throw new InvalidOperationException("Canonical audit event payload deserialized to null.");
        return FromCanonical(canonical);
    }

    private static CanonicalAuditEvent ToCanonical(AuditEvent e) => new(
        e.Id,
        (int)e.Channel,
        e.EventType.Key,
        e.SchemaVersion,
        e.OccurredAt.ToUniversalTime().ToUnixTimeMilliseconds(),
        (int)e.Severity,
        ToCanonical(e.Actor),
        e.Targets.Select(ToCanonical).ToArray(),
        new CanonicalOutcome((int)e.Outcome.Status, e.Outcome.Reason?.Key),
        new CanonicalClientContext(e.Client.IpAddress, e.Client.UserAgent, e.Client.Asn, e.Client.GeoLocation),
        new CanonicalAuthContext(
            e.AuthenticationContext.Sid,
            e.AuthenticationContext.Jti,
            e.AuthenticationContext.Acr,
            e.AuthenticationContext.Amr.ToArray(),
            e.AuthenticationContext.AuthTime?.ToUniversalTime().ToUnixTimeMilliseconds(),
            e.AuthenticationContext.Protocol),
        new CanonicalCorrelation(e.Correlation.RequestId, e.Correlation.TraceId, e.Correlation.TransactionId),
        e.Changes.Select(ToCanonical).ToArray());

    private static CanonicalActor ToCanonical(Actor a) => new(
        (int)a.Type, a.Id, a.Scope.IsPlatform, a.Scope.TenantId,
        a.OnBehalfOf is null ? null : ToCanonical(a.OnBehalfOf));

    private static CanonicalTarget ToCanonical(AuditTarget t) => new(t.Type.Key, t.Id, t.CreatedVia?.Key);

    private static CanonicalPropertyChange ToCanonical(PropertyChange c) => new(
        c.PropertyName, ToCanonical(c.OldValue), ToCanonical(c.NewValue));

    private static CanonicalAuditValue? ToCanonical(AuditValue? value) => value switch
    {
        null => null,
        AuditValue.Plain plain => new CanonicalAuditValue(0, plain.DisplayValue, null, null),
        AuditValue.Redacted redacted => new CanonicalAuditValue(1, null, redacted.Value.Hash, redacted.Value.Prefix),
        _ => throw new NotSupportedException($"Unknown audit value kind: {value.GetType()}")
    };

    private static AuditEvent FromCanonical(CanonicalAuditEvent c)
    {
        var actor = FromCanonicalActor(c.Actor);
        var targets = c.Targets.Select(FromCanonicalTarget).ToArray();
        var outcomeReason = c.Outcome.Reason is null ? null : ResolveOutcomeReason(c.Outcome.Reason);
        var outcome = (AuditResult)c.Outcome.Status == AuditResult.Success
            ? AuditOutcome.Success(outcomeReason)
            : AuditOutcome.Failure(outcomeReason!);
        var client = Unwrap(ClientContext.Create(c.Client.IpAddress, c.Client.UserAgent, c.Client.Asn, c.Client.GeoLocation));
        var authenticationContext = AuthContext.Create(
            c.AuthenticationContext.Sid,
            c.AuthenticationContext.Jti,
            c.AuthenticationContext.Acr,
            c.AuthenticationContext.Amr,
            c.AuthenticationContext.AuthTimeUnixMs is { } authTimeMs
                ? DateTimeOffset.FromUnixTimeMilliseconds(authTimeMs)
                : null,
            c.AuthenticationContext.Protocol);
        var correlation = Correlation.Create(c.Correlation.RequestId, c.Correlation.TraceId, c.Correlation.TransactionId);
        var changes = c.Changes.Select(FromCanonicalPropertyChange).ToArray();
        var eventType = AuditEventType.FromKey(c.EventType)
            ?? throw new InvalidOperationException($"Unknown audit event type key: '{c.EventType}'.");

        return Unwrap(AuditEvent.Create(
            c.Id,
            (AuditChannel)c.Channel,
            eventType,
            c.SchemaVersion,
            DateTimeOffset.FromUnixTimeMilliseconds(c.OccurredAtUnixMs),
            (AuditSeverity)c.Severity,
            actor,
            outcome,
            client,
            authenticationContext,
            correlation,
            targets,
            changes));
    }

    private static Actor FromCanonicalActor(CanonicalActor c) => Unwrap(Actor.Create(
        (ActorType)c.Type,
        c.Id,
        c.IsPlatform ? ActorScope.Platform() : ActorScope.ForTenant(c.TenantId!.Value),
        c.OnBehalfOf is null ? null : FromCanonicalActor(c.OnBehalfOf)));

    private static AuditOutcomeReason ResolveOutcomeReason(string key)
        => AuditOutcomeReason.FromKey(key) ?? throw new InvalidOperationException($"Unknown audit outcome reason key: '{key}'.");

    private static AuditTarget FromCanonicalTarget(CanonicalTarget c) => Unwrap(AuditTarget.Create(
        AuditTargetType.FromKey(c.Type) ?? throw new InvalidOperationException($"Unknown audit target type key: '{c.Type}'."),
        c.Id,
        c.CreatedVia is null
            ? null
            : AuditCreationSource.FromKey(c.CreatedVia) ?? throw new InvalidOperationException($"Unknown audit creation source key: '{c.CreatedVia}'.")));

    private static PropertyChange FromCanonicalPropertyChange(CanonicalPropertyChange c) => Unwrap(PropertyChange.Create(
        c.PropertyName, FromCanonicalAuditValue(c.OldValue), FromCanonicalAuditValue(c.NewValue)));

    private static AuditValue? FromCanonicalAuditValue(CanonicalAuditValue? c) => c switch
    {
        null => null,
        { Kind: 0 } => new AuditValue.Plain(c.DisplayValue!),
        { Kind: 1 } => new AuditValue.Redacted(ProtectedValue.FromTrustedSource(c.Hash!, c.Prefix)),
        _ => throw new NotSupportedException($"Unknown canonical audit value kind: {c.Kind}")
    };

    private static T Unwrap<T>(Result<T> result) => result.IsSuccess
        ? result.Value
        : throw new InvalidOperationException(
            $"Corrupt canonical audit payload: {string.Join("; ", result.Errors.Select(e => e.Message))}");
}
