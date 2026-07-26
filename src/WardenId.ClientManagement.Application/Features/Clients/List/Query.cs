using FluentResults;
using MediatR;
using WardenId.ClientManagement.Domain;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.ClientManagement.Application.Features.Clients.List;

public sealed record Query(
    Guid TenantId,
    int PageNumber,
    int PageSize,
    bool? Enabled,
    ClientType? ClientType) : IRequest<Result<PaginationListResponse<ClientListItem>>>;
