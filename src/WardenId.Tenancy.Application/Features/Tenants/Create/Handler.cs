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
 
        var tenantResult = Tenant.Create(slug, request.DisplayName);
        if (tenantResult.IsFailed) return  Result.Fail(tenantResult.Errors);
 
        var tenant = tenantResult.Value;
 
        // Pre-generate the key id so the "kid" embedded in the JWK by the generator matches
        // TenantSigningKey.Id once the entity is created — see ISigningKeyGenerator's doc.
        var signingKeyId = Guid.NewGuid();
        var keyMaterial = await signingKeyGenerator.GenerateEs256Async(signingKeyId, cancellationToken);
        var signingKey = SigningKey.CreateEs256(
            signingKeyId, keyMaterial.PublicKeyJwk, keyMaterial.EncryptedPrivateKey);
 
        var assignResult = tenant.AssignInitialSigningKey(signingKey);
        if (assignResult.IsFailed) return Result.Fail(assignResult.Errors);
 
        var activateResult = tenant.Activate();
        if (activateResult.IsFailed) return Result.Fail(activateResult.Errors);
 
        await repository.CreateAsync(tenant, request.UserId, cancellationToken);
 
        return Result.Ok(tenant.Id);
    }
}