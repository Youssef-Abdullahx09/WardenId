using System.Data;
using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Domain;
using WardenId.Audit.Infrastructure.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Audit.Infrastructure.Sealing;

/// <summary>
/// Raw SQL is unavoidable here (spec §12 / docs/audit/PLAN.md §3.8) — EF doesn't expose
/// pg_snapshot_xmin/pg_current_snapshot or the xmin system column, and the InMemory provider
/// can't execute FromSqlInterpolated at all. This class is a thin, Postgres-only wrapper with
/// no unit test of its own; it's exercised via the real database, not this test suite.
/// The watermark is kept as text throughout (not ulong) so it round-trips through an Npgsql
/// text parameter and an explicit ::xid8 cast without fighting .NET's lack of a native
/// unsigned-bigint mapping.
/// </summary>
public sealed class PgSealableEventReader : ISealableEventReader
{
    public async Task<IReadOnlyList<AuditEventRecord>> GetSealableEventsAsync(
        AuditDbContext db, AuditChannel channel, Guid? tenantId, int limit, CancellationToken cancellationToken = default)
    {
        var watermark = await GetWatermarkAsync(db, cancellationToken);
        var channelName = channel.ToString();

        return await db.AuditEvents.FromSqlInterpolated($"""
            SELECT * FROM audit.events
            WHERE "Channel" = {channelName}
              AND "TenantId" IS NOT DISTINCT FROM {tenantId}
              AND "Sequence" IS NULL
              AND xmin::text::xid8 < {watermark}::xid8
            ORDER BY "Id" ASC
            LIMIT {limit}
            """).ToListAsync(cancellationToken);
    }

    private static async Task<string> GetWatermarkAsync(AuditDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_snapshot_xmin(pg_current_snapshot())::text";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (string)result!;
    }
}
