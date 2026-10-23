using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys;

namespace WardenId.Tenancy.Application.Features.SigningKeys.GetPublicKeys;

/// <summary>
/// Public keys for discovery/JWKS. Only Active and Suspended tenants are servable — suspension
/// is enforced where tokens are issued, not where they're verified. Every ineligible case returns
/// the same NotFound so the response never reveals that a tenant exists in another state.
/// </summary>
public sealed class Handler(ITenancyDbContext dbContext) : IRequestHandler<Query, Result<TenantPublicKeys>>
{
    public async Task<Result<TenantPublicKeys>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Route constraint has already enforced slug format; FromTrustedSource only builds the
        // comparison value for the EF value converter.
        var slug = Slug.FromTrustedSource(request.Slug);

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .Include(t => t.SigningKeys)
            .Where(t => t.Slug == slug)
            .Where(t => t.Status == Status.Active || t.Status == Status.Suspended)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null) return Result.Fail(TenantErrors.NotFound);

        // Retiring keys stay published so tokens signed before a rotation still verify;
        // Retired keys are withdrawn.
        var jwks = tenant.SigningKeys
            .Where(k => k.IsPublished)
            .Select(k => k.PublicKeyJwk)
            .ToList();

        return Result.Ok(new TenantPublicKeys(tenant.Id, tenant.Slug.Value, jwks));
    }
}
