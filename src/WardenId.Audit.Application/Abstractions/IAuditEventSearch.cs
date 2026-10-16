using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Abstractions;

/// <summary>
/// What the events table can filter on. Every filter is an exact match on an indexed column; the
/// search columns (event type, actor, targets, outcome) are empty on rows whose payload was purged
/// before they existed, so those rows only match the channel/tenant/time filters.
/// </summary>
/// <param name="EventType">Taxonomy key, e.g. "admin.entity.update".</param>
/// <param name="Actor">Either the scope-qualified actor ("platform:adminuser:{id}" / "tenant:{tid}:{type}:{id}") or just the actor's id.</param>
/// <param name="Target">One target as "{type key}:{id}", e.g. "user:{id}"; matches events that have it among their targets.</param>
public sealed record AuditEventFilter(
    AuditChannel? Channel,
    Guid? TenantId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? EventType = null,
    string? Actor = null,
    string? Target = null,
    AuditResult? Outcome = null);

/// <summary>
/// An events-table row as the search returns it. Payload is null once retention removed it; the
/// search columns are null (Targets empty) where they couldn't be filled.
/// </summary>
public sealed record AuditEventSearchRow(
    Guid Id,
    AuditChannel Channel,
    Guid? TenantId,
    long? Sequence,
    DateTimeOffset OccurredAt,
    byte[]? Payload,
    string? EventType,
    string? Actor,
    IReadOnlyList<string> Targets,
    AuditResult? Outcome);

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

    Task<AuditEventSearchRow?> FindAsync(Guid eventId, CancellationToken cancellationToken = default);
}
