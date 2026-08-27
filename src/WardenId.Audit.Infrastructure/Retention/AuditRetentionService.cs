using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WardenId.Audit.Domain;
using WardenId.Audit.Domain.Retention;
using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Retention;

/// <summary>
/// Hash-preserving purge (spec §4 I6 / §9 phase 5) — nulls AuditEventRecord.Payload once
/// RetentionPolicy.IsPurgeable says a row is old enough AND its covering segment has already
/// been exported (docs/audit/OPEN-DECISIONS.md #6). LeafHash is never touched, so roots stay
/// recomputable and sequence density still detects deletion (I6). Never DELETEs (I6, enforced
/// again defense-in-depth by the append-only trigger in the AddSegmentExportedAtAndAppendOnlyTrigger
/// migration).
///
/// Unlike AuditSealerService, no advisory lock: nulling an already-null Payload is a no-op, so
/// two replicas racing the same segment is harmless rather than something that needs excluding.
/// </summary>
public sealed class AuditRetentionService(
    IAuditRetentionDbContextFactory dbContextFactory,
    IOptions<AuditRetentionOptions> options,
    ILogger<AuditRetentionService> logger) : BackgroundService
{
    private AuditRetentionOptions Options => options.Value;

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
                logger.LogError(ex, "Audit retention pass failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One sweep over every channel's exported segments. Public: the unit of work tests drive directly.</summary>
    public async Task RunPassAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var channel in Enum.GetValues<AuditChannel>())
        {
            var retentionWindow = Options.RetentionWindowFor(channel);
            var channelName = channel.ToString();

            var exportedSegments = await db.AuditSegments
                .Where(s => s.Channel == channelName && s.ExportedAt != null)
                .ToListAsync(cancellationToken);

            foreach (var segment in exportedSegments)
                await PurgeSegmentAsync(db, segment, retentionWindow, now, cancellationToken);
        }
    }

    private static async Task PurgeSegmentAsync(
        AuditDbContext db, AuditSegmentRecord segment, TimeSpan retentionWindow, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidates = await db.AuditEvents
            .Where(e => e.Channel == segment.Channel && e.TenantId == segment.TenantId
                && e.Sequence >= segment.SequenceStart && e.Sequence <= segment.SequenceEnd
                && e.Payload != null)
            .ToListAsync(cancellationToken);

        var purgeable = candidates
            .Where(e => RetentionPolicy.IsPurgeable(e.OccurredAt, now, retentionWindow, isExported: true))
            .ToList();

        if (purgeable.Count == 0) return;

        foreach (var row in purgeable) row.Payload = null;
        await db.SaveChangesAsync(cancellationToken);
    }
}
