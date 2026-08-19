using FluentResults;
using MediatR;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Features.Tenants.Create;

public sealed class Handler(
    ITenantRepository repository,
    ISigningKeyGenerator signingKeyGenerator) : IRequestHandler<Command, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        var slugResult = Slug.Create(request.Slug);
        if (slugResult.IsFailed) return Result.Fail(slugResult.Errors); 
 
        var slug = slugResult.Value;
 
        if (await repository.ExistsBySlugAsync(slug, cancellationToken))
            return Result.Fail(TenantErrors.TenantSlugAlreadyExists(slug));
 
        var tenantResult = TenantProvisioning.CreateActivated(slug, request.DisplayName, signingKeyGenerator);
        if (tenantResult.IsFailed) return Result.Fail(tenantResult.Errors);

        var tenant = tenantResult.Value;

        await repository.CreateAsync(tenant, request.UserId, cancellationToken);
 
        return Result.Ok(tenant.Id);
    }
}