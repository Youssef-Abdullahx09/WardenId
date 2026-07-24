namespace WardenId.Tenancy.Application.Abstractions;

/// <summary>
/// PublicKeyJwk is a serialized JWK (kty, crv, x, y, kid, alg, use) ready to drop into a
/// JWKS response as-is. EncryptedPrivateKey is PKCS8 key material protected at rest —
/// only ever decrypted inside the Protocol module, at token-signing time.
/// </summary>
public sealed record SigningKeyMaterial(string PublicKeyJwk, byte[] EncryptedPrivateKey);