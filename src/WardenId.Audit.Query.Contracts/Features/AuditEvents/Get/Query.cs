namespace WardenId.Audit.Query.Contracts.Features.AuditEvents.Get;

/// <summary>One audit event by id, whatever its channel.</summary>
public sealed record Query(Guid EventId);
