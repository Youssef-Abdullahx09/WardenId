using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Application.Abstractions;
using WardenId.Audit.Domain;
using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure;

/// <summary>Filters on indexed columns only (channel, tenant, occurred-at); newest first.</summary>
public sealed class AuditEventSearch(IDbContextFactory<AuditDbContext> dbContextFactory) : IAuditEventSearch
{
    public async Task<(long Total, IReadOnlyList<AuditEventSearchRow> Rows)> SearchAsync(
        AuditEventFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.AuditEvents.AsNoTracking();
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

        var total = await query.LongCountAsync(cancellationToken);

        var records = await query
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new { e.Id, e.Channel, e.TenantId, e.Sequence, e.OccurredAt, e.Payload })
            .ToListAsync(cancellationToken);

        return (total, records
            .Select(e => new AuditEventSearchRow(
                e.Id, Enum.Parse<AuditChannel>(e.Channel), e.TenantId, e.Sequence, e.OccurredAt, e.Payload))
            .ToArray());
    }
}
