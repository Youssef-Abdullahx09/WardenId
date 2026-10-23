using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Application.Abstractions;
using WardenId.Audit.Domain;
using WardenId.Audit.Infrastructure.Persistence;
using WardenId.Audit.Infrastructure.Sealing;

namespace WardenId.Audit.Infrastructure;

/// <summary>
/// Everything here reads only already-sealed data (audit.segments/checkpoints, and
/// audit.events rows with Sequence assigned) — plain LINQ throughout, no raw SQL/xmin needed
/// (that watermark logic only matters for the sealer's own not-yet-sealed candidate query).
/// Fully portable across providers, unlike PgSealableEventReader/PgChannelAdvisoryLock.
/// </summary>
public sealed class AuditProofRepository(IDbContextFactory<AuditDbContext> dbContextFactory) : IAuditProofRepository
{
    public async Task<AuditEventRow?> FindEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.AuditEvents.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        return record is null
            ? null
            : new AuditEventRow(record.Id, Enum.Parse<AuditChannel>(record.Channel), record.TenantId, record.Sequence, record.Payload, record.LeafHash);
    }

    public async Task<SegmentRow?> FindCoveringSegmentAsync(
        AuditChannel channel, Guid? tenantId, long sequence, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var channelName = channel.ToString();
        var record = await db.AuditSegments.FirstOrDefaultAsync(
            s => s.Channel == channelName && s.TenantId == tenantId
                 && s.SequenceStart <= sequence && sequence <= s.SequenceEnd,
            cancellationToken);
        return record is null ? null : ToSegmentRow(record);
    }

    public async Task<IReadOnlyList<byte[]>> GetSegmentLeafHashesAsync(
        AuditChannel channel, Guid? tenantId, long sequenceStart, long sequenceEnd, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var channelName = channel.ToString();
        return await db.AuditEvents
            .Where(e => e.Channel == channelName && e.TenantId == tenantId
                        && e.Sequence != null && e.Sequence >= sequenceStart && e.Sequence <= sequenceEnd)
            .OrderBy(e => e.Sequence)
            .Select(e => e.LeafHash)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SegmentRow>> GetSegmentsAsync(
        AuditChannel channel, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var channelName = channel.ToString();
        var records = await db.AuditSegments
            .Where(s => s.Channel == channelName && s.TenantId == tenantId)
            .OrderBy(s => s.SequenceStart)
            .ToListAsync(cancellationToken);
        return records.Select(ToSegmentRow).ToList();
    }

    public async Task<IReadOnlyList<CheckpointRow>> GetCheckpointsAsync(
        AuditChannel channel, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var channelName = channel.ToString();
        var records = await db.AuditCheckpoints
            .Where(c => c.Channel == channelName && c.TenantId == tenantId)
            .OrderBy(c => c.TreeSize)
            .ToListAsync(cancellationToken);
        return records
            .Select(c => new CheckpointRow(c.Id, c.SegmentId, c.TreeSize, c.RootHash, c.Kid, c.Signature, c.SignedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<CheckpointKeyRow>> GetCheckpointKeysAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await db.AuditCheckpointSigningKeys.ToListAsync(cancellationToken);
        return records
            .OrderBy(k => k.CreatedAt)
            .Select(k => new CheckpointKeyRow(
                k.Id.ToString(CheckpointSigningKeyFactory.KidFormat), k.PublicKeyJwk, k.IsActive, k.CreatedAt,
                k.PreviousKeyId?.ToString(CheckpointSigningKeyFactory.KidFormat), k.RotationAttestation, k.DeactivatedAt))
            .ToList();
    }

    private static SegmentRow ToSegmentRow(AuditSegmentRecord record) =>
        new(record.Id, record.SequenceStart, record.SequenceEnd,
            record.MerkleRoot, record.PreviousHeaderHash, record.HeaderHash, record.SealedAt);
}
