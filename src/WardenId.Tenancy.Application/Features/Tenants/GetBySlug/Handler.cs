using FluentResults;
using MediatR;
using WardenId.Tenancy.Application.Features.Tenants.Get;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Query.Contracts;
using TenantsGetBySlugQuery = WardenId.Tenancy.Query.Contracts.Features.Tenants.GetBySlug.Query;

namespace WardenId.Tenancy.Application.Features.Tenants.GetBySlug;

/// <summary>Read side: the tenant comes from the Dapper query layer (Tenancy.Query), not the aggregate.</summary>
public sealed class Handler(ITenantsQuery tenantsQuery) : IRequestHandler<Query, Result<TenantDetail>>
{
    public async Task<Result<TenantDetail>> Handle(Query request, CancellationToken cancellationToken)
    {
        // A malformed slug can't name a tenant: answer "not found", not a validation error.
        var slug = Slug.Create(request.Slug);
        if (slug.IsFailed) return Result.Fail(TenantErrors.NotFound);

        var result = await tenantsQuery.GetBySlug(new TenantsGetBySlugQuery(slug.Value.Value), cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        return result.Value is { } item ? Result.Ok(TenantDetail.From(item)) : Result.Fail(TenantErrors.NotFound);
    }
}
