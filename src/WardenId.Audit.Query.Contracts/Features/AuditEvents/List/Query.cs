namespace WardenId.Audit.Query.Contracts.Features.AuditEvents.List;

/// <summary>
/// One page of audit events, newest <c>OccurredAt</c> first. Every filter is optional (null = any) and an exact match;
/// the search columns (event type, actor, targets, outcome) are empty on rows whose payload was purged before they
/// existed, so those rows only match the channel/tenant/time filters.
/// </summary>
/// <param name="Channel">The <c>AuditChannel</c> member name, as persisted.</param>
/// <param name="TenantId">Only this tenant's events; null for all, platform events included.</param>
/// <param name="From">Inclusive lower bound on <c>OccurredAt</c>.</param>
/// <param name="To">Inclusive upper bound on <c>OccurredAt</c>.</param>
/// <param name="EventType">Taxonomy key, e.g. "admin.entity.update".</param>
/// <param name="Actor">The scope-qualified actor or just the actor's id; matches either column.</param>
/// <param name="Target">One target as "{type key}:{id}"; matches events that have it among their targets.</param>
/// <param name="Outcome">The <c>AuditResult</c> member name, as persisted.</param>
public sealed record Query(
    string? Channel,
    Guid? TenantId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? EventType,
    string? Actor,
    string? Target,
    string? Outcome,
    int PageNumber,
    int PageSize);
