using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Audit.Query.Contracts.Features.AuditEvents.List;

/// <summary>
/// One page of audit events. Every filter is optional (null = any) and an exact match; the search columns (event type,
/// actor, targets, outcome) are empty on rows whose payload was purged before they existed, so those rows only match
/// the channel/tenant/time filters.
/// </summary>
public sealed record Query(
    int PageNumber,
    int PageSize,
    SortDirection SortDirection) : PaginationListRequest(PageNumber, PageSize, SortDirection)
{
    public AuditChannel? Channel { get; init; }

    /// <summary>Only this tenant's events; null for all, platform events included.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Inclusive lower bound on <c>OccurredAt</c>.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Inclusive upper bound on <c>OccurredAt</c>.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Taxonomy key, e.g. "admin.entity.update"; trimmed, null or blank for no filter.</summary>
    public string? EventType { get; init; }

    /// <summary>The scope-qualified actor or just the actor's id, matching either column; trimmed, null or blank for no filter.</summary>
    public string? Actor { get; init; }

    /// <summary>One target as "{type key}:{id}", matching events that have it among their targets; trimmed, null or blank for no filter.</summary>
    public string? Target { get; init; }

    public AuditResult? Outcome { get; init; }

    public SortOption SortOption { get; init; } = SortOption.OccurredAt;
}

/// <summary>The column an audit event page is ordered by; the event id breaks ties in the same direction.</summary>
public enum SortOption
{
    OccurredAt = 1,

    /// <summary>The event's position in its channel's log; only ordered within one channel, unsealed events (no sequence yet) last.</summary>
    Sequence = 2
}
