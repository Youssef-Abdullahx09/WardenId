using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Features.SigningKeys.GetActiveForSigning;

public sealed class Handler(ITenancyDbContext dbContext) : IRequestHandler<Query, Result<ActiveSigningKey>>
{
    public async Task<Result<ActiveSigningKey>> Handle(Query request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .Include(t => t.SigningKeys)
            .Where(t => t.Id == request.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null) return Result.Fail(TenantErrors.NotFound);

        var activeKey = tenant.SigningKeys.FirstOrDefault(k => k.IsActive);
        if (activeKey is null) return Result.Fail(TenantErrors.NoActiveSigningKeyForSigning());

        return Result.Ok(new ActiveSigningKey(
            activeKey.SigningKeyId, activeKey.Algorithm, activeKey.EncryptedPrivateKey, tenant.Slug.Value));
    }
}
