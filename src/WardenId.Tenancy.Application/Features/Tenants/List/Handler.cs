using FluentResults;
using MediatR;
using WardenId.SharedKernel.Query.Contracts;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Query.Contracts;
using WardenId.Tenancy.Query.Contracts.Features.Tenants.List;
using TenantsListQuery = WardenId.Tenancy.Query.Contracts.Features.Tenants.List.Query;

namespace WardenId.Tenancy.Application.Features.Tenants.List;

/// <summary>Read side: tenants come from the Dapper query layer (Tenancy.Query), not the aggregate.</summary>
public sealed class Handler(ITenantsQuery tenantsQuery) : IRequestHandler<Query, Result<PaginationListResponse<Item>>>
{
    public async Task<Result<PaginationListResponse<Item>>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Minimal-API enum binding also accepts numbers, so an undefined member can get this far.
        if (!Enum.IsDefined(request.SortOption) || !Enum.IsDefined(request.SortDirection))
            return Result.Fail(TenantErrors.Validation(
                $"SortOption must be one of {string.Join(", ", Enum.GetNames<SortOption>())} " +
                $"and SortDirection one of {string.Join(", ", Enum.GetNames<SortDirection>())}."));

        return await tenantsQuery.List(
            new TenantsListQuery(request.PageNumber, request.PageSize, request.SortDirection)
            {
                Status = request.Status,
                SortOption = request.SortOption
            },
            cancellationToken);
    }
}
