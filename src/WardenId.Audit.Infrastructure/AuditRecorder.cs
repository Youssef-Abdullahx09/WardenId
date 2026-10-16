using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WardenId.Audit.Application;
using WardenId.Audit.Domain;
using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure;

/// <summary>
/// Standalone insert — no ambient transaction assumed (spec §5). Fails open (I10): every
/// exception is caught and logged, never rethrown, so an audit write failure can never break
/// the caller's actual work (e.g. a login).
/// </summary>
public sealed class AuditRecorder(IDbContextFactory<AuditDbContext> dbContextFactory, ILogger<AuditRecorder> logger)
    : IAuditRecorder
{
    public async Task RecordAsync(AuditEvent auditEvent, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var record = AuditEventRecordFactory.Create(auditEvent, tenantId);

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            db.AuditEvents.Add(record);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Failed to record audit event {EventId} ({EventType})", auditEvent.Id, auditEvent.EventType.Key);
        }
    }
}
