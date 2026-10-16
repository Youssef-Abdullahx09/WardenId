using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Application.Abstractions;
using WardenId.Audit.Domain;
using WardenId.Audit.Infrastructure.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Audit.Infrastructure;

/// <summary>Filters on indexed columns only (channel, tenant, occurred-at and the search columns); newest first.</summary>
public sealed class AuditEventSearch(IDbContextFactory<AuditDbContext> dbContextFactory) : IAuditEventSearch
{
    public async Task<(long Total, IReadOnlyList<AuditEventSearchRow> Rows)> SearchAsync(
        AuditEventFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = Filter(db.AuditEvents.AsNoTracking(), filter);

        var total = await query.LongCountAsync(cancellationToken);

        var records = await query
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (total, records.Select(ToRow).ToArray());
    }

    public async Task<AuditEventSearchRow?> FindAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.AuditEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        return record is null ? null : ToRow(record);
    }

    private static IQueryable<AuditEventRecord> Filter(IQueryable<AuditEventRecord> query, AuditEventFilter filter)
    {
        if (filter.Channel is { } channel)
        {
            var channelName = channel.ToString();
            query = query.Where(e => e.Channel == channelName);
        }
        if (filter.TenantId is { } tenantId)
            query = query.Where(e => e.TenantId == tenantId);
        if (filter.From is { } from)
            query = query.Where(e => e.OccurredAt >= from);
        if (filter.To is { } to)
            query = query.Where(e => e.OccurredAt <= to);
        if (filter.EventType is { } eventType)
            query = query.Where(e => e.EventType == eventType);
        if (filter.Actor is { } actor)
            query = query.Where(e => e.Actor == actor || e.ActorId == actor);
        if (filter.Target is { } target)
            query = query.Where(e => e.Targets != null && e.Targets.Contains(target));
        if (filter.Outcome is { } outcome)
        {
            var outcomeName = outcome.ToString();
            query = query.Where(e => e.Outcome == outcomeName);
        }
        return query;
    }

    private static AuditEventSearchRow ToRow(AuditEventRecord e) => new(
        e.Id,
        Enum.Parse<AuditChannel>(e.Channel),
        e.TenantId,
        e.Sequence,
        e.OccurredAt,
        e.Payload,
        e.EventType,
        e.Actor,
        e.Targets ?? [],
        Enum.TryParse<AuditResult>(e.Outcome, out var outcome) ? outcome : null);
}
