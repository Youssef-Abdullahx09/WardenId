using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Infrastructure.Persistence;

namespace WardenId.Audit.Infrastructure.Sealing;

/// <summary>
/// pg_try_advisory_lock/pg_advisory_unlock on the given AuditDbContext's own connection —
/// advisory locks are session-scoped, so the acquire and release (and every query run while
/// "holding" it) must share that same physical connection. Same untestable-without-Postgres
/// caveat as PgXminWatermarkReader.
/// </summary>
public sealed class PgChannelAdvisoryLock : IChannelAdvisoryLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(
        AuditDbContext db, long lockKey, CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var acquireCommand = connection.CreateCommand();
        acquireCommand.CommandText = "SELECT pg_try_advisory_lock(@key)";
        AddParameter(acquireCommand, "key", lockKey);

        var acquired = (bool)(await acquireCommand.ExecuteScalarAsync(cancellationToken))!;
        return acquired ? new Handle(connection, lockKey) : null;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed class Handle(DbConnection connection, long lockKey) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (connection.State != ConnectionState.Open) return;

            await using var releaseCommand = connection.CreateCommand();
            releaseCommand.CommandText = "SELECT pg_advisory_unlock(@key)";
            AddParameter(releaseCommand, "key", lockKey);
            await releaseCommand.ExecuteScalarAsync();
        }
    }
}
