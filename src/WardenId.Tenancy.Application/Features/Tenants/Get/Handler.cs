using FluentResults;
using MediatR;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Query.Contracts;
using TenantsGetQuery = WardenId.Tenancy.Query.Contracts.Features.Tenants.Get.Query;

namespace WardenId.Tenancy.Application.Features.Tenants.Get;

/// <summary>Read side: the tenant comes from the Dapper query layer (Tenancy.Query), not the aggregate.</summary>
public sealed class Handler(ITenantsQuery tenantsQuery) : IRequestHandler<Query, Result<TenantDetail>>
{
    public async Task<Result<TenantDetail>> Handle(Query request, CancellationToken cancellationToken)
    {
        var result = await tenantsQuery.Get(new TenantsGetQuery(request.TenantId), cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        return result.Value is { } item ? Result.Ok(TenantDetail.From(item)) : Result.Fail(TenantErrors.NotFound);
    }
}
