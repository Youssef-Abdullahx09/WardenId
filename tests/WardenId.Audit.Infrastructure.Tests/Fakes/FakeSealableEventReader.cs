using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Infrastructure.Sealing;

namespace WardenId.Audit.Infrastructure.Tests.Fakes;

/// <summary>
/// Stands in for PgSealableEventReader in tests: plain LINQ over the (InMemory) DbSet,
/// skipping the real xmin-watermark filter entirely — that's the one piece genuinely
/// untestable without a real Postgres connection (see PgSealableEventReader's doc comment).
/// Tests that need to prove "rows above the watermark are excluded" construct a reader that
/// only returns a subset instead of using this one.
/// </summary>
public sealed class FakeSealableEventReader : ISealableEventReader
{
    public async Task<IReadOnlyList<AuditEventRecord>> GetSealableEventsAsync(
        AuditDbContext db, AuditChannel channel, Guid? tenantId, int limit, CancellationToken cancellationToken = default)
        => await db.AuditEvents
            .Where(e => e.Channel == channel.ToString() && e.TenantId == tenantId && e.Sequence == null)
            .OrderBy(e => e.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
