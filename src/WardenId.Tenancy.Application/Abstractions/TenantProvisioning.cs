using FluentResults;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Abstractions;

/// <summary>
/// Shared "create + generate signing key + activate" sequence used by both the HTTP-driven
/// Create-Tenant handler and the master-tenant startup seed, so the two paths can't drift.
/// Does not persist — callers still own <see cref="ITenantRepository.CreateAsync"/> since the
/// audit actor id differs per caller (authenticated user vs. the system-seeded case).
/// </summary>
public static class TenantProvisioning
{
    public static Result<Tenant> CreateActivated(
        Slug slug,
        string displayName,
        ISigningKeyGenerator signingKeyGenerator)
    {
        var tenantResult = Tenant.Create(slug, displayName);
        if (tenantResult.IsFailed) return Result.Fail(tenantResult.Errors);

        var tenant = tenantResult.Value;

        // Pre-generate the key id so the "kid" embedded in the JWK by the generator matches
        // TenantSigningKey.Id once the entity is created — see ISigningKeyGenerator's doc.
        var signingKeyId = Guid.NewGuid();
        var keyMaterial = signingKeyGenerator.GenerateEs256Async(signingKeyId);
        var signingKey = SigningKey.CreateEs256(
            signingKeyId, keyMaterial.PublicKeyJwk, keyMaterial.EncryptedPrivateKey);

        var assignResult = tenant.AssignInitialSigningKey(signingKey);
        if (assignResult.IsFailed) return Result.Fail(assignResult.Errors);

        var activateResult = tenant.Activate();
        if (activateResult.IsFailed) return Result.Fail(activateResult.Errors);

        return Result.Ok(tenant);
    }
}
