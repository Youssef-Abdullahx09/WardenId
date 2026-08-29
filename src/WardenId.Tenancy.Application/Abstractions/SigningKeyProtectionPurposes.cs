namespace WardenId.Tenancy.Application.Abstractions;

/// <summary>
/// Data Protection purpose string used to encrypt/decrypt tenant signing-key private key
/// material. Shared verbatim with WardenId.Protocol.Infrastructure, which decrypts the key
/// at token-signing time (see SigningKeyMaterial's doc comment) — bump the version suffix on
/// both sides together if the protection scheme ever changes incompatibly.
/// </summary>
public static class SigningKeyProtectionPurposes
{
    public const string PrivateKey = "WardenId.Tenancy.SigningKeys.v1";
}
