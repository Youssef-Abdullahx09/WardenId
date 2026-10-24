using FluentResults;
using MediatR;
using WardenId.Audit.Query.Contracts.Features.AuditEvents;
using WardenId.Audit.Query.Contracts.Features.AuditEvents.List;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Audit.Application.Features.Events.List;

public sealed record Query(
    int PageNumber,
    int PageSize,
    SortDirection SortDirection) : IRequest<Result<PaginationListResponse<AuditEventListItem>>>
{
    public AuditChannel? Channel { get; init; }

    /// <summary>Only this tenant's events; null for all, platform events included.</summary>
    public Guid? TenantId { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    /// <summary>Taxonomy key, e.g. "admin.entity.update"; blank means no filter.</summary>
    public string? EventType { get; init; }

    /// <summary>The scope-qualified actor ("platform:adminuser:{id}" / "tenant:{tid}:{type}:{id}") or just the actor's id; blank means no filter.</summary>
    public string? Actor { get; init; }

    /// <summary>One target as "{type key}:{id}"; blank means no filter.</summary>
    public string? Target { get; init; }

    /// <summary>Success or Failure.</summary>
    public AuditResult? Outcome { get; init; }

    public SortOption SortOption { get; init; } = SortOption.OccurredAt;
}
