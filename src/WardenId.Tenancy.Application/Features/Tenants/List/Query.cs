using FluentResults;
using MediatR;
using WardenId.SharedKernel.Query.Contracts;
using WardenId.Tenancy.Query.Contracts.Features.Tenants;
using WardenId.Tenancy.Query.Contracts.Features.Tenants.List;

namespace WardenId.Tenancy.Application.Features.Tenants.List;

public sealed record Query(
    int PageNumber,
    int PageSize,
    SortDirection SortDirection) : IRequest<Result<PaginationListResponse<Item>>>
{
    public Status? Status { get; init; }

    public SortOption SortOption { get; init; } = SortOption.CreatedAt;
}
