using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace WardenId.Audit.Infrastructure.Sealing;

/// <summary>Key generation and signing shared by <see cref="Es256CheckpointSigner"/> and <see cref="Es256CheckpointKeyRotator"/>.</summary>
internal static class CheckpointSigningKeyFactory
{
    /// <summary>A new active P-256 key; X and Y are the base64url public point as written in its JWK.</summary>
    public static (AuditCheckpointSigningKeyRecord Record, string X, string Y) Create(IDataProtector protector)
    {
        var id = Guid.CreateVersion7();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        var x = Base64UrlEncode(parameters.Q.X!);
        var y = Base64UrlEncode(parameters.Q.Y!);
        var publicKeyJwk = JsonSerializer.Serialize(new
        {
            kty = "EC",
            crv = "P-256",
            x,
            y,
            alg = "ES256",
            use = "sig",
            kid = id.ToString("N")
        });

        var privateKeyPkcs8 = ecdsa.ExportPkcs8PrivateKey();
        try
        {
            var record = new AuditCheckpointSigningKeyRecord
            {
                Id = id,
                PublicKeyJwk = publicKeyJwk,
                EncryptedPrivateKey = protector.Protect(privateKeyPkcs8),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            return (record, x, y);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyPkcs8);
        }
    }

    /// <summary>ES256 over <paramref name="data"/>, IEEE P1363 (raw r||s) — the JOSE shape.</summary>
    public static byte[] Sign(IDataProtector protector, AuditCheckpointSigningKeyRecord key, byte[] data)
    {
        var privateKeyPkcs8 = protector.Unprotect(key.EncryptedPrivateKey);
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);
            return ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyPkcs8);
        }
    }

    public static string Kid(AuditCheckpointSigningKeyRecord key) => key.Id.ToString("N");

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
