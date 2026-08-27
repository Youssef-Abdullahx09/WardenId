using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WardenId.Audit.Domain;
using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Exporting;

/// <summary>
/// First real CAP producer for audit data (spec §5's "Exporter → CAP publish sealed segments to
/// external sinks"). Publish and marking AuditSegmentRecord.ExportedAt land in one EF
/// transaction (via IAuditSegmentPublisher enlisting CAP's outbox insert onto it) so "exported"
/// can never be true without the message having genuinely landed in CAP's own durable outbox —
/// see docs/audit/PLAN.md §6b.
/// </summary>
public sealed class AuditExporterService(
    IDbContextFactory<AuditDbContext> dbContextFactory,
    IAuditSegmentPublisher publisher,
    IOptions<AuditExporterOptions> options,
    ILogger<AuditExporterService> logger) : BackgroundService
{
    private AuditExporterOptions Options => options.Value;

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
                logger.LogError(ex, "Audit exporter pass failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One sweep over every unexported sealed segment. Public: the unit of work tests drive directly.</summary>
    public async Task RunPassAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var pending = await db.AuditSegments
            .Where(s => s.ExportedAt == null)
            .OrderBy(s => s.SealedAt)
            .ToListAsync(cancellationToken);

        foreach (var segment in pending)
        {
            try
            {
                await ExportSegmentAsync(db, segment, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to export audit segment {SegmentId}", segment.Id);
            }
        }
    }

    private async Task ExportSegmentAsync(AuditDbContext db, AuditSegmentRecord segment, CancellationToken cancellationToken)
    {
        var events = await db.AuditEvents
            .Where(e => e.Channel == segment.Channel && e.TenantId == segment.TenantId
                && e.Sequence >= segment.SequenceStart && e.Sequence <= segment.SequenceEnd)
            .OrderBy(e => e.Sequence)
            .ToListAsync(cancellationToken);

        var expectedCount = segment.SequenceEnd - segment.SequenceStart + 1;
        if (events.Count != expectedCount)
        {
            logger.LogWarning(
                "Segment {SegmentId} covers {Expected} events but only {Found} are visible yet; skipping export this pass",
                segment.Id, expectedCount, events.Count);
            return;
        }

        var message = new AuditSegmentExportedMessage
        {
            SegmentId = segment.Id,
            Channel = (int)Enum.Parse<AuditChannel>(segment.Channel),
            TenantId = segment.TenantId,
            SequenceStart = segment.SequenceStart,
            SequenceEnd = segment.SequenceEnd,
            MerkleRoot = segment.MerkleRoot,
            PreviousHeaderHash = segment.PreviousHeaderHash,
            HeaderHash = segment.HeaderHash,
            SealedAt = segment.SealedAt,
            Events = events.Select(e => new AuditExportedEvent
            {
                Id = e.Id,
                Sequence = e.Sequence!.Value,
                OccurredAt = e.OccurredAt,
                Payload = e.Payload
                    ?? throw new InvalidOperationException(
                        $"Event {e.Id} has no payload to export — retention must never purge a row before its segment is exported."),
                LeafHash = e.LeafHash
            }).ToList()
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await publisher.PublishAsync(db, message, cancellationToken);
        segment.ExportedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
