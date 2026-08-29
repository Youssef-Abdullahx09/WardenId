using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Features.SigningKeys.GetActiveForSigning;

public sealed class Handler(ITenancyDbContext dbContext) : IRequestHandler<Query, Result<ActiveSigningKeyDto>>
{
    public async Task<Result<ActiveSigningKeyDto>> Handle(Query request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .Include(t => t.SigningKeys)
            .Where(t => t.Id == request.TenantId && !t.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null) return Result.Fail(TenantErrors.NotFound);

        var activeKey = tenant.SigningKeys.FirstOrDefault(k => k.IsActive && !k.IsDeleted);
        if (activeKey is null) return Result.Fail(TenantErrors.NoActiveSigningKeyForSigning());

        return Result.Ok(new ActiveSigningKeyDto(activeKey.SigningKeyId, activeKey.Algorithm, activeKey.EncryptedPrivateKey));
    }
}
