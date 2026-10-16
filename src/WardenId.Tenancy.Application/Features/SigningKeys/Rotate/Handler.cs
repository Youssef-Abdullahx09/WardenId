using FluentResults;
using MediatR;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Features.SigningKeys.Rotate;

public sealed class Handler(
    ITenantRepository repository,
    ISigningKeyGenerator signingKeyGenerator) : IRequestHandler<Command, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
    {
        var tenantResult = await repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenantResult.IsFailed) return Result.Fail(tenantResult.Errors);

        var tenant = tenantResult.Value;

        // Same pre-generated id as TenantProvisioning, so the JWK "kid" matches SigningKeyId.
        var signingKeyId = Guid.NewGuid();
        var keyMaterial = signingKeyGenerator.GenerateEs256Async(signingKeyId);
        var signingKey = SigningKey.CreateEs256(
            signingKeyId, keyMaterial.PublicKeyJwk, keyMaterial.EncryptedPrivateKey);

        var result = tenant.RotateSigningKey(signingKey);
        if (result.IsFailed) return Result.Fail(result.Errors);

        await repository.UpdateAsync(tenant, request.UserId, cancellationToken);
        return Result.Ok(signingKeyId);
    }
}
