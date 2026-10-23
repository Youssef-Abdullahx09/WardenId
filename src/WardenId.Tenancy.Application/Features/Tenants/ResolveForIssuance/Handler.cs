using FluentResults;
using MediatR;
using WardenId.Tenancy.Application.Utilities;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;
using WardenId.Tenancy.Query.Contracts;
using TenantsResolveQuery = WardenId.Tenancy.Query.Contracts.Features.Tenants.ResolveForIssuance.Query;

namespace WardenId.Tenancy.Application.Features.Tenants.ResolveForIssuance;

// Inside this namespace, a bare \"Query\" would otherwise bind to the WardenId.Tenancy.Query namespace.
using Query = WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance.Query;

/// <summary>
/// Same eligibility as GetPublicKeys (Active or Suspended; everything else is an indistinguishable
/// NotFound), without loading keys. The caller decides what Suspended means.
/// </summary>
public sealed class Handler(ITenantsQuery tenantsQuery) : IRequestHandler<Query, Result<TenantForIssuance>>
{
    public async Task<Result<TenantForIssuance>> Handle(Query request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Slug)) return Result.Fail(TenantErrors.NotFound);

        var result = await tenantsQuery.ResolveForIssuance(new TenantsResolveQuery(request.Slug), cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        var status = result.Value is { } item ? QueryRows.TryParseStatus(item.Status) : null;
        TenantIssuanceStatus? issuanceStatus = status switch
        {
            Status.Active => TenantIssuanceStatus.Active,
            Status.Suspended => TenantIssuanceStatus.Suspended,
            _ => null
        };
        if (issuanceStatus is null) return Result.Fail(TenantErrors.NotFound);

        var tenant = result.Value!;
        return Result.Ok(new TenantForIssuance(tenant.Id, tenant.Slug, issuanceStatus.Value, tenant.DisplayName));
    }
}
