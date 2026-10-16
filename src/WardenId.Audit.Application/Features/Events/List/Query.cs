using FluentResults;
using MediatR;
using WardenId.Audit.Domain;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Audit.Application.Features.Events.List;

/// <param name="TenantId">Only this tenant's events; null for all, platform events included.</param>
/// <param name="EventType">Taxonomy key, e.g. "admin.entity.update".</param>
/// <param name="Actor">The scope-qualified actor ("platform:adminuser:{id}" / "tenant:{tid}:{type}:{id}") or just the actor's id.</param>
/// <param name="Target">One target as "{type key}:{id}".</param>
/// <param name="Outcome">Success or Failure.</param>
public sealed record Query(
    int PageNumber,
    int PageSize,
    AuditChannel? Channel,
    Guid? TenantId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? EventType,
    string? Actor,
    string? Target,
    AuditResult? Outcome) : IRequest<Result<PaginationListResponse<AuditEventListItem>>>;
