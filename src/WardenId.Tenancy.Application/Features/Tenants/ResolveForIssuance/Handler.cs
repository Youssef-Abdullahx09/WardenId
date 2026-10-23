using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;

namespace WardenId.Tenancy.Application.Features.Tenants.ResolveForIssuance;

/// <summary>
/// Same eligibility as GetPublicKeys (Active or Suspended, not deleted; everything else is an
/// indistinguishable NotFound), without loading keys. The caller decides what Suspended means.
/// </summary>
public sealed class Handler(ITenancyDbContext dbContext) : IRequestHandler<Query, Result<TenantForIssuance>>
{
    public async Task<Result<TenantForIssuance>> Handle(Query request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Slug)) return Result.Fail(TenantErrors.NotFound);

        var slug = Slug.FromTrustedSource(request.Slug);

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Slug == slug)
            .Where(t => t.Status == Status.Active || t.Status == Status.Suspended)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null) return Result.Fail(TenantErrors.NotFound);

        var status = tenant.Status == Status.Suspended ? TenantIssuanceStatus.Suspended : TenantIssuanceStatus.Active;
        return Result.Ok(new TenantForIssuance(tenant.Id, tenant.Slug.Value, status, tenant.DisplayName));
    }
}
