namespace WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys;

/// <summary>
/// Public signing material for a tenant that is eligible to be served via discovery/JWKS.
/// PublicKeyJwks are the stored JWK JSON strings, verbatim — never private key material.
/// </summary>
public sealed record TenantPublicKeys(Guid TenantId, string Slug, IReadOnlyList<string> PublicKeyJwks);
