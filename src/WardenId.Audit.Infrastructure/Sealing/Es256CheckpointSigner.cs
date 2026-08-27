using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Sealing;

/// <summary>
/// ES256 (ECDSA P-256), sign-only, dedicated audit key distinct from Tenancy's token-signing
/// key (spec §6). Deliberately reuses the app's single shared DataProtection key ring rather
/// than a dedicated one — see docs/audit/OPEN-DECISIONS.md's key-storage decision: calling
/// services.AddDataProtection().PersistKeysToDbContext&lt;T&gt;() from two modules would silently
/// let whichever module registers last win the entire app's XmlRepository. This module's own
/// purpose string still makes the derived key material cryptographically distinct from
/// Tenancy's, even though both live behind the same physical keyring.
///
/// Signature format is IEEE P1363 (raw r||s, 64 bytes) — the standard JOSE ES256 shape —
/// even though the checkpoint body itself is a signed-note, not a JWS, so a verifier written
/// against either convention recognizes the format.
/// </summary>
public sealed class Es256CheckpointSigner(
    IDataProtectionProvider dataProtectionProvider,
    IDbContextFactory<AuditDbContext> dbContextFactory) : IAuditCheckpointSigner
{
    // Bump the version suffix if the protection scheme ever changes incompatibly — existing
    // encrypted keys become undecryptable across a purpose-string change.
    private const string DataProtectionPurpose = "WardenId.Audit.CheckpointKeys.v1";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);

    public async Task<(string Kid, byte[] Signature)> SignAsync(byte[] unsignedBody, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var keyRecord = await GetOrCreateActiveKeyAsync(db, cancellationToken);

        var privateKeyPkcs8 = _protector.Unprotect(keyRecord.EncryptedPrivateKey);
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);
            var signature = ecdsa.SignData(unsignedBody, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return (keyRecord.Id.ToString("N"), signature);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyPkcs8);
        }
    }

    private async Task<AuditCheckpointSigningKeyRecord> GetOrCreateActiveKeyAsync(
        AuditDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.AuditCheckpointSigningKeys
            .SingleOrDefaultAsync(k => k.IsActive, cancellationToken);
        if (existing is not null) return existing;

        var created = CreateKeyRecord();
        db.AuditCheckpointSigningKeys.Add(created);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException)
        {
            // Lost the race to another sealer replica generating the first key concurrently
            // (enforced by the unique-active-key index) — re-read the winner's row instead.
            db.Entry(created).State = EntityState.Detached;
            return await db.AuditCheckpointSigningKeys.SingleAsync(k => k.IsActive, cancellationToken);
        }
    }

    private AuditCheckpointSigningKeyRecord CreateKeyRecord()
    {
        var id = Guid.CreateVersion7();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var publicKeyJwk = ExportPublicJwk(ecdsa, id);
        var privateKeyPkcs8 = ecdsa.ExportPkcs8PrivateKey();
        try
        {
            return new AuditCheckpointSigningKeyRecord
            {
                Id = id,
                PublicKeyJwk = publicKeyJwk,
                EncryptedPrivateKey = _protector.Protect(privateKeyPkcs8),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }
        finally
        {
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
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
