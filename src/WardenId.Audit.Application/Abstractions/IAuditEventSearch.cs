using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Abstractions;

/// <summary>What the events table can filter on: everything else lives inside the payload.</summary>
public sealed record AuditEventFilter(
    AuditChannel? Channel,
    Guid? TenantId,
    DateTimeOffset? From,
    DateTimeOffset? To);

/// <summary>An events-table row as the search returns it. Payload is null once retention removed it.</summary>
public sealed record AuditEventSearchRow(
    Guid Id,
    AuditChannel Channel,
    Guid? TenantId,
    long? Sequence,
    DateTimeOffset OccurredAt,
    byte[]? Payload);

/// <summary>
/// Pages through audit events, newest first. Read-only, so like <see cref="IAuditProofRepository"/>
/// it opens its own context per call instead of exposing the audit DbContext.
/// </summary>
public interface IAuditEventSearch
{
    Task<(long Total, IReadOnlyList<AuditEventSearchRow> Rows)> SearchAsync(
        AuditEventFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
