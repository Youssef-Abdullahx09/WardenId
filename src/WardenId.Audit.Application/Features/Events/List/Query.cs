using FluentResults;
using MediatR;
using WardenId.Audit.Domain;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Audit.Application.Features.Events.List;

/// <param name="TenantId">Only this tenant's events; null for all, platform events included.</param>
public sealed record Query(
    int PageNumber,
    int PageSize,
    AuditChannel? Channel,
    Guid? TenantId,
    DateTimeOffset? From,
    DateTimeOffset? To) : IRequest<Result<PaginationListResponse<AuditEventListItem>>>;
