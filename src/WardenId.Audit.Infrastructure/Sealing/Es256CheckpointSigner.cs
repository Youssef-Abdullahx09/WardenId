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
    internal const string DataProtectionPurpose = "WardenId.Audit.CheckpointKeys.v1";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);

    public async Task<(string Kid, byte[] Signature)> SignAsync(byte[] unsignedBody, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var keyRecord = await GetOrCreateActiveKeyAsync(db, cancellationToken);

        return (CheckpointSigningKeyFactory.Kid(keyRecord), CheckpointSigningKeyFactory.Sign(_protector, keyRecord, unsignedBody));
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

    private AuditCheckpointSigningKeyRecord CreateKeyRecord() => CheckpointSigningKeyFactory.Create(_protector).Record;
}
