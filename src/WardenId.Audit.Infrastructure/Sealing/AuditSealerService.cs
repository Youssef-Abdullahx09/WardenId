using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WardenId.Audit.Application.Abstractions;
using WardenId.Audit.Domain;
using WardenId.Audit.Domain.Sealing;
using WardenId.Audit.Domain.Utilities;
using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Sealing;

/// <summary>
/// The one BackgroundService in this codebase (spec §9 phase 3 / docs/audit/PLAN.md §3.10 —
/// no existing pattern to match). One active instance per (channel, tenant) across all
/// replicas via <see cref="IChannelAdvisoryLock"/>; may lag arbitrarily without affecting
/// write availability — events are queryable the instant they commit, sealing only adds the
/// dense sequence and Merkle chaining on top, later.
///
/// Deliberately independent per (channel, tenant): docs/audit/OPEN-DECISIONS.md #2 — no
/// cross-channel/cross-tenant anchoring is built here.
/// </summary>
public sealed class AuditSealerService(
    IDbContextFactory<AuditDbContext> dbContextFactory,
    ISealableEventReader sealableEventReader,
    IChannelAdvisoryLock advisoryLock,
    IAuditCheckpointSigner checkpointSigner,
    IEnumerable<IAuditCheckpointWitness> witnesses,
    IOptions<AuditSealerOptions> options,
    ILogger<AuditSealerService> logger) : BackgroundService
{
    private AuditSealerOptions Options => options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Options.PollInterval);
        do
        {
            try
            {
                await RunPassAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Audit sealer pass failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One sweep over every (channel, tenant) with unsealed rows. Public: also the unit of work tests drive directly instead of running the polling loop.</summary>
    public async Task RunPassAsync(CancellationToken cancellationToken)
    {
        List<(string Channel, Guid? TenantId)> scopes;
        await using (var db = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            // Materialize the anonymous-type projection first, then convert to tuples
            // in-memory — chaining a ValueTuple.Create projection before ToListAsync isn't
            // translatable by the Npgsql provider (it only worked here against InMemory,
            // which evaluates far more leniently than a real relational provider).
            var rows = await db.AuditEvents
                .Where(e => e.Sequence == null)
                .Select(e => new { e.Channel, e.TenantId })
                .Distinct()
                .ToListAsync(cancellationToken);
            scopes = rows.Select(r => (r.Channel, r.TenantId)).ToList();
        }

        foreach (var (channel, tenantId) in scopes)
            await ProcessScopeAsync(Enum.Parse<AuditChannel>(channel), tenantId, cancellationToken);
    }

    private async Task ProcessScopeAsync(AuditChannel channel, Guid? tenantId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var lockKey = ComputeLockKey(channel, tenantId);

        await using var handle = await advisoryLock.TryAcquireAsync(db, lockKey, cancellationToken);
        if (handle is null) return; // another replica already owns this (channel, tenant) pair

        await SealIfDueAsync(db, channel, tenantId, cancellationToken);
        await CheckpointIfDueAsync(db, channel, tenantId, cancellationToken);
    }

    private async Task SealIfDueAsync(AuditDbContext db, AuditChannel channel, Guid? tenantId, CancellationToken cancellationToken)
    {
        var candidates = await sealableEventReader.GetSealableEventsAsync(
            db, channel, tenantId, Options.MaxSegmentSize, cancellationToken);

        if (candidates.Count == 0) return;

        var sinceOldestPending = DateTimeOffset.UtcNow - candidates[0].OccurredAt;
        if (!SegmentSealPolicy.ShouldSeal(candidates.Count, sinceOldestPending, Options.MaxSegmentSize, Options.MaxSegmentAge))
            return;

        var channelName = channel.ToString();
        var lastSegment = await db.AuditSegments
            .Where(s => s.Channel == channelName && s.TenantId == tenantId)
            .OrderByDescending(s => s.SequenceEnd)
            .FirstOrDefaultAsync(cancellationToken);

        var sequenceStart = (lastSegment?.SequenceEnd ?? 0) + 1;
        var merkleRoot = MerkleTree.ComputeRoot(candidates.Select(c => c.LeafHash).ToList());

        var segmentResult = AuditSegment.Create(
            Guid.CreateVersion7(), channel, tenantId, sequenceStart, sequenceStart + candidates.Count - 1,
            merkleRoot, lastSegment?.HeaderHash, DateTimeOffset.UtcNow);

        if (segmentResult.IsFailed)
        {
            logger.LogError(
                "Failed to build audit segment for {Channel}/{TenantId}: {Errors}",
                channel, tenantId, string.Join("; ", segmentResult.Errors));
            return;
        }

        var segment = segmentResult.Value;
        for (var i = 0; i < candidates.Count; i++)
            candidates[i].Sequence = sequenceStart + i;

        db.AuditSegments.Add(new AuditSegmentRecord
        {
            Id = segment.Id,
            Channel = segment.Channel.ToString(),
            TenantId = segment.TenantId,
            SequenceStart = segment.SequenceStart,
            SequenceEnd = segment.SequenceEnd,
            MerkleRoot = segment.MerkleRoot,
            PreviousHeaderHash = segment.PreviousHeaderHash,
            HeaderHash = segment.HeaderHash,
            SealedAt = segment.SealedAt
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task CheckpointIfDueAsync(AuditDbContext db, AuditChannel channel, Guid? tenantId, CancellationToken cancellationToken)
    {
        var channelName = channel.ToString();
        var allSegments = await db.AuditSegments
            .Where(s => s.Channel == channelName && s.TenantId == tenantId)
            .OrderBy(s => s.SequenceStart)
            .ToListAsync(cancellationToken);

        if (allSegments.Count == 0) return;

        var lastCheckpoint = await db.AuditCheckpoints
            .Where(c => c.Channel == channelName && c.TenantId == tenantId)
            .OrderByDescending(c => c.SignedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var previousTreeSize = lastCheckpoint?.TreeSize ?? 0;
        var newSegmentCount = allSegments.Count - (int)previousTreeSize;
        if (newSegmentCount <= 0) return;

        var sinceLastCheckpoint = lastCheckpoint is null ? TimeSpan.MaxValue : DateTimeOffset.UtcNow - lastCheckpoint.SignedAt;
        if (!CheckpointDuePolicy.IsDue(newSegmentCount, sinceLastCheckpoint, Options.MaxSegmentsPerCheckpoint, Options.MaxCheckpointInterval))
            return;

        var treeSize = allSegments.Count;
        var rootHash = MerkleTree.ComputeRoot(allSegments.Select(s => s.HeaderHash).ToList());
        var scopeLabel = tenantId?.ToString() ?? AuditFormats.Scopes.Platform;

        var unsignedBody = AuditCheckpoint.BuildUnsignedBody(channel, scopeLabel, treeSize, rootHash);
        var (kid, signature) = await checkpointSigner.SignAsync(unsignedBody, cancellationToken);

        var checkpointResult = AuditCheckpoint.Create(
            Guid.CreateVersion7(), channel, tenantId, treeSize, rootHash, kid, signature, DateTimeOffset.UtcNow);

        if (checkpointResult.IsFailed)
        {
            logger.LogError(
                "Failed to build audit checkpoint for {Channel}/{TenantId}: {Errors}",
                channel, tenantId, string.Join("; ", checkpointResult.Errors));
            return;
        }

        var checkpoint = checkpointResult.Value;
        db.AuditCheckpoints.Add(new AuditCheckpointRecord
        {
            Id = checkpoint.Id,
            SegmentId = allSegments[^1].Id,
            Channel = checkpoint.Channel.ToString(),
            TenantId = checkpoint.TenantId,
            TreeSize = checkpoint.TreeSize,
            RootHash = checkpoint.RootHash,
            Kid = checkpoint.Kid,
            Signature = checkpoint.Signature,
            SignedAt = checkpoint.SignedAt
        });

        await db.SaveChangesAsync(cancellationToken);

        await WitnessCheckpointAsync(checkpoint, scopeLabel, cancellationToken);
    }

    /// <summary>
    /// Best-effort (spec §8) — a witness failure must never undo or block the checkpoint, which
    /// already committed above as the authoritative signed record.
    /// </summary>
    private async Task WitnessCheckpointAsync(AuditCheckpoint checkpoint, string scopeLabel, CancellationToken cancellationToken)
    {
        if (!witnesses.Any()) return;

        var signedNoteText = checkpoint.ToSignedNoteText(scopeLabel);
        foreach (var witness in witnesses)
        {
            try
            {
                await witness.WitnessAsync(signedNoteText, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Checkpoint witness {Witness} failed for checkpoint {CheckpointId}", witness.GetType().Name, checkpoint.Id);
            }
        }
    }

    private static long ComputeLockKey(AuditChannel channel, Guid? tenantId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{(int)channel}:{tenantId}"));
        return BitConverter.ToInt64(hash, 0);
    }
}
