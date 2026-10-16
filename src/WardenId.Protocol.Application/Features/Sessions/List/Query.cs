using FluentResults;
using MediatR;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Protocol.Application.Features.Sessions.List;

/// <param name="UserId">Only this user's sessions; null for every user of the tenant.</param>
/// <param name="ClientId">Only sessions this client (public client_id) took part in; null for any.</param>
/// <param name="IncludeEnded">Also ended and expired sessions; otherwise live ones only.</param>
public sealed record Query(
    Guid TenantId,
    int PageNumber,
    int PageSize,
    Guid? UserId,
    string? ClientId,
    bool IncludeEnded) : IRequest<Result<PaginationListResponse<SessionListItem>>>;
