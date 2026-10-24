using FluentResults;
using MediatR;
using WardenId.ClientManagement.Query.Contracts.Features.Clients;
using WardenId.ClientManagement.Query.Contracts.Features.Clients.List;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.ClientManagement.Application.Features.Clients.List;

public sealed record Query(
    Guid TenantId,
    int PageNumber,
    int PageSize,
    SortDirection SortDirection) : IRequest<Result<PaginationListResponse<Item>>>
{
    public bool? Enabled { get; init; }

    public ClientType? ClientType { get; init; }

    public SortOption SortOption { get; init; } = SortOption.CreatedAt;
}
