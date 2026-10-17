using FluentResults;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WardenId.Audit.Application.Abstractions;
using WardenId.Audit.Domain;
using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Sealing;

/// <summary>
/// Rotates the checkpoint key Es256CheckpointSigner signs with (spec §6). One save holds the old key's
/// deactivation, the new key with the old key's signature over its CheckpointKeyRotation statement, and
/// the rotation's own audit event — all or nothing (I10). The old key is kept, inactive, so checkpoints
/// it signed keep verifying. Two concurrent rotations both try to insert an active key; the partial
/// unique index lets one win and the other gets a conflict. A checkpoint being signed meanwhile uses
/// whichever key was active when it read, and either verifies.
///
/// The signed statement then goes to the configured checkpoint witnesses, best effort like checkpoints
/// (spec §8): a witness that holds the old key's checkpoints can follow the hand-over without trusting
/// the database's key table.
/// </summary>
public sealed class Es256CheckpointKeyRotator(
    IDataProtectionProvider dataProtectionProvider,
    IDbContextFactory<AuditDbContext> dbContextFactory,
    IEnumerable<IAuditCheckpointWitness> witnesses,
    ILogger<Es256CheckpointKeyRotator> logger) : IAuditCheckpointKeyRotator
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(Es256CheckpointSigner.DataProtectionPurpose);

    public async Task<Result<CheckpointKeyRotationResult>> RotateAsync(
        Func<string?, string, AuditEvent> buildAuditEvent, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var previous = await db.AuditCheckpointSigningKeys.SingleOrDefaultAsync(k => k.IsActive, cancellationToken);

        var (next, x, y) = CheckpointSigningKeyFactory.Create(_protector);
        var nextKid = CheckpointSigningKeyFactory.Kid(next);
        var previousKid = previous is null ? null : CheckpointSigningKeyFactory.Kid(previous);
        string? signedNote = null;

        if (previous is not null)
        {
            var statement = CheckpointKeyRotation.BuildUnsignedBody(previousKid!, nextKid, x, y);
            var attestation = CheckpointSigningKeyFactory.Sign(_protector, previous, statement);
            next.PreviousKeyId = previous.Id;
            next.RotationAttestation = attestation;
            previous.IsActive = false;
            previous.DeactivatedAt = DateTimeOffset.UtcNow;
            signedNote = CheckpointKeyRotation.ToSignedNoteText(previousKid!, nextKid, x, y, attestation);
        }

        db.AuditCheckpointSigningKeys.Add(next);
        db.AuditEvents.Add(AuditEventRecordFactory.Create(buildAuditEvent(previousKid, nextKid), tenantId: null));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Checkpoint key rotation lost to a concurrent rotation; nothing was changed.");
            return Result.Fail(AuditErrors.CheckpointKeyRotationConflict());
        }

        if (signedNote is not null) await WitnessAsync(signedNote, nextKid, cancellationToken);

        return Result.Ok(new CheckpointKeyRotationResult(nextKid, previousKid));
    }

    private async Task WitnessAsync(string signedNote, string kid, CancellationToken cancellationToken)
    {
        foreach (var witness in witnesses)
        {
            try
            {
                await witness.WitnessAsync(signedNote, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Checkpoint witness {Witness} failed for the rotation to key {Kid}", witness.GetType().Name, kid);
            }
        }
    }
}
