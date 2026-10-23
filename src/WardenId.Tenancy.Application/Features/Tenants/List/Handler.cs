using FluentResults;
using MediatR;
using WardenId.SharedKernel.Query.Contracts;
using WardenId.Tenancy.Application.Utilities;
using WardenId.Tenancy.Query.Contracts;
using TenantsListQuery = WardenId.Tenancy.Query.Contracts.Features.Tenants.List.Query;

namespace WardenId.Tenancy.Application.Features.Tenants.List;

/// <summary>Read side: tenants come from the Dapper query layer (Tenancy.Query), not the aggregate.</summary>
public sealed class Handler(ITenantsQuery tenantsQuery)
    : IRequestHandler<Query, Result<PaginationListResponse<TenantListItem>>>
{
    public async Task<Result<PaginationListResponse<TenantListItem>>> Handle(
        Query request,
        CancellationToken cancellationToken)
    {
        var result = await tenantsQuery.List(
            new TenantsListQuery(request.PageNumber, request.PageSize, request.Status?.ToString()),
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        var items = result.Value.Data
            .Select(item => new TenantListItem(
                item.Id,
                item.Slug,
                item.DisplayName,
                QueryRows.ParseStatus(item.Status),
                QueryRows.AsUtc(item.CreatedAt)))
            .ToList();

        return Result.Ok(new PaginationListResponse<TenantListItem>(result.Value.RecordsTotal, items));
    }
}
