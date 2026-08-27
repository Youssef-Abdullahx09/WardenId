using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Exporting;

/// <summary>
/// Seam around the actual CAP publish call, purely for testability — no hand-rolled ICapPublisher
/// fake is practical (same reasoning as ISealableEventReader/IChannelAdvisoryLock's split in
/// phase 3). Must be called with a DbContext that already has an open transaction: the real
/// implementation enlists CAP's own outbox insert onto that same transaction so "published" and
/// "marked exported" commit or roll back together.
/// </summary>
public interface IAuditSegmentPublisher
{
    Task PublishAsync(AuditDbContext db, AuditSegmentExportedMessage message, CancellationToken cancellationToken = default);
}
