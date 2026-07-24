namespace WardenId.Tenancy.Application.Abstractions;

/// <summary>
/// Generates signing key material for a tenant. The key id is supplied by the caller —
/// pre-generated before the TenantSigningKey entity exists — so the "kid" embedded in the
/// returned JWK matches the entity's Id once it's persisted and served via JWKS.
///
/// Swappable for a Vault/KMS-backed implementation later (FR-OP-06) without touching any
/// Application-layer code, since callers only ever depend on this interface.
/// </summary>
public interface ISigningKeyGenerator
{
    SigningKeyMaterial GenerateEs256Async(Guid keyId);
}