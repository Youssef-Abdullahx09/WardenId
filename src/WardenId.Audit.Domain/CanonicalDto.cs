using System.Text.Json.Serialization;

namespace WardenId.Audit.Domain;

// Wire shapes for CanonicalAuditEventSerializer only. JsonPropertyOrder is pinned explicitly
// rather than left to declaration/reflection order, and every timestamp is epoch milliseconds
// rather than an ISO string — both exist so canonical bytes stay byte-identical across process
// restarts and unrelated migrations (I1/I2).

internal sealed record CanonicalAuditEvent(
    [property: JsonPropertyOrder(0)] Guid Id,
    [property: JsonPropertyOrder(1)] int Channel,
    [property: JsonPropertyOrder(2)] string EventType,
    [property: JsonPropertyOrder(3)] int SchemaVersion,
    [property: JsonPropertyOrder(4)] long OccurredAtUnixMs,
    [property: JsonPropertyOrder(5)] int Severity,
    [property: JsonPropertyOrder(6)] CanonicalActor Actor,
    [property: JsonPropertyOrder(7)] CanonicalTarget[] Targets,
    [property: JsonPropertyOrder(8)] CanonicalOutcome Outcome,
    [property: JsonPropertyOrder(9)] CanonicalClientContext Client,
    [property: JsonPropertyOrder(10)] CanonicalAuthContext AuthenticationContext,
    [property: JsonPropertyOrder(11)] CanonicalCorrelation Correlation,
    [property: JsonPropertyOrder(12)] CanonicalPropertyChange[] Changes);

internal sealed record CanonicalActor(
    [property: JsonPropertyOrder(0)] int Type,
    [property: JsonPropertyOrder(1)] string Id,
    [property: JsonPropertyOrder(2)] bool IsPlatform,
    [property: JsonPropertyOrder(3)] Guid? TenantId,
    [property: JsonPropertyOrder(4)] CanonicalActor? OnBehalfOf);

internal sealed record CanonicalTarget(
    [property: JsonPropertyOrder(0)] string Type,
    [property: JsonPropertyOrder(1)] string Id,
    [property: JsonPropertyOrder(2)] string? CreatedVia);

internal sealed record CanonicalOutcome(
    [property: JsonPropertyOrder(0)] int Status,
    [property: JsonPropertyOrder(1)] string? Reason);

internal sealed record CanonicalClientContext(
    [property: JsonPropertyOrder(0)] string? IpAddress,
    [property: JsonPropertyOrder(1)] string? UserAgent,
    [property: JsonPropertyOrder(2)] string? Asn,
    [property: JsonPropertyOrder(3)] string? GeoLocation);

internal sealed record CanonicalAuthContext(
    [property: JsonPropertyOrder(0)] string? Sid,
    [property: JsonPropertyOrder(1)] string? Jti,
    [property: JsonPropertyOrder(2)] string? Acr,
    [property: JsonPropertyOrder(3)] string[] Amr,
    [property: JsonPropertyOrder(4)] long? AuthTimeUnixMs,
    [property: JsonPropertyOrder(5)] string? Protocol);

internal sealed record CanonicalCorrelation(
    [property: JsonPropertyOrder(0)] string? RequestId,
    [property: JsonPropertyOrder(1)] string? TraceId,
    [property: JsonPropertyOrder(2)] Guid? TransactionId);

internal sealed record CanonicalPropertyChange(
    [property: JsonPropertyOrder(0)] string PropertyName,
    [property: JsonPropertyOrder(1)] CanonicalAuditValue? OldValue,
    [property: JsonPropertyOrder(2)] CanonicalAuditValue? NewValue);

internal sealed record CanonicalAuditValue(
    [property: JsonPropertyOrder(0)] int Kind,
    [property: JsonPropertyOrder(1)] string? DisplayValue,
    [property: JsonPropertyOrder(2)] string? Hash,
    [property: JsonPropertyOrder(3)] string? Prefix);
