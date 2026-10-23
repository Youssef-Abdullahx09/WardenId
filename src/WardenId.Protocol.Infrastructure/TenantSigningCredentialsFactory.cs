using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Tenancy.Application.Abstractions;

namespace WardenId.Protocol.Infrastructure;

/// <summary>
/// The only place a tenant's private signing key is decrypted (protected by Tenancy with the shared
/// SigningKeyProtectionPurposes.PrivateKey purpose). Used by the OpenIddict
/// AttachTenantSigningCredentials handler (token endpoint).
/// </summary>
public sealed class TenantSigningCredentialsFactory(IDataProtectionProvider dataProtectionProvider)
    : ITenantSigningCredentialsFactory
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector(SigningKeyProtectionPurposes.PrivateKey);

    public SigningCredentials Create(ActiveSigningKey key)
    {
        if (key.Algorithm != SecurityAlgorithms.EcdsaSha256)
            throw new NotSupportedException($"Unsupported signing key algorithm: '{key.Algorithm}'. Only ES256 is supported.");

        var privateKeyPkcs8 = _protector.Unprotect(key.EncryptedPrivateKey);
        try
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);

            return new SigningCredentials(
                new ECDsaSecurityKey(ecdsa) { KeyId = key.KeyId.ToString("N") },
                SecurityAlgorithms.EcdsaSha256);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyPkcs8);
        }
    }
}
