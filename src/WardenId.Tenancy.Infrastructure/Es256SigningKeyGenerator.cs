using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using WardenId.Tenancy.Application.Abstractions;

namespace WardenId.Tenancy.Infrastructure;

/// <summary>
/// Generates ES256 (ECDSA P-256) signing keys and protects the private key at rest via
/// ASP.NET Core Data Protection.
/// </summary>
public sealed class Es256SigningKeyGenerator(
    IDataProtectionProvider dataProtectionProvider) : ISigningKeyGenerator
{
    // Bump the version suffix if the protection scheme ever changes incompatibly —
    // existing encrypted keys become undecryptable across a purpose-string change.
    private const string DataProtectionPurpose = "WardenId.Tenancy.SigningKeys.v1";
 
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);

    public SigningKeyMaterial GenerateEs256Async(Guid keyId)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
 
        var publicKeyJwk = ExportPublicJwk(ecdsa, keyId);
        var privateKeyPkcs8 = ecdsa.ExportPkcs8PrivateKey();

        try
        {
            var encryptedPrivateKey = _protector.Protect(privateKeyPkcs8);
            return new SigningKeyMaterial(publicKeyJwk, encryptedPrivateKey);
        }
        finally
        {
            // Private key material shouldn't linger in memory longer than it has to.
            CryptographicOperations.ZeroMemory(privateKeyPkcs8);
        }
    }
 
    private static string ExportPublicJwk(ECDsa ecdsa, Guid keyId)
    {
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
 
        var jwk = new
        {
            kty = "EC",
            crv = "P-256",
            x = Base64UrlEncode(parameters.Q.X!),
            y = Base64UrlEncode(parameters.Q.Y!),
            alg = "ES256",
            use = "sig",
            kid = keyId.ToString("N")
        };
 
        return JsonSerializer.Serialize(jwk);
    }
 
    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}