using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using WardenId.Audit.Infrastructure.Migrations;
using WardenId.Audit.Infrastructure.Sealing;
using VerifyHandler = WardenId.Audit.Application.Features.Verification.Verify.Handler;
using VerifyQuery = WardenId.Audit.Application.Features.Verification.Verify.Query;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>
/// The search columns on audit.events (event type, actor, targets, outcome) sit next to the hashed
/// payload. These tests run the real migrations on Postgres and prove the backfill reads the stored
/// payload, leaves purged rows alone, and never touches the bytes the Merkle chain is built from.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AuditEventSearchColumnsMigrationTests(PostgresFixture postgres)
{
    private const string MigrationBeforeSearchColumns = "20260822170046_ChannelAsString";

    private static readonly Guid Acme = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static int _sequence;

    private static AuditEvent MakeEvent(AuditEventType type, Actor actor, AuditOutcome outcome, params AuditTarget[] targets) =>
        AuditEvent.Create(
            new Guid(Interlocked.Increment(ref _sequence), 0, 0, new byte[8]),
            AuditChannel.AdminConfig, type, 1, DateTimeOffset.UtcNow.AddMinutes(-10), AuditSeverity.Info,
            actor, outcome, ClientContext.Unknown(), AuthContext.None(), Correlation.None(), targets).Value;

    private static AuditEvent TenantEvent() => MakeEvent(
        AuditEventType.AdminTenantSuspend,
        Actor.Create(ActorType.AdminUser, "admin-1", ActorScope.ForTenant(Acme)).Value,
        AuditOutcome.Failure(AuditOutcomeReason.PolicyDenied),
        AuditTarget.Create(AuditTargetType.Tenant, Acme.ToString()).Value,
        AuditTarget.Create(AuditTargetType.User, "user-1").Value);

    private static AuditEvent PlatformEvent() => MakeEvent(
        AuditEventType.AdminEntityUpdate,
        Actor.Create(
            ActorType.ServiceAccount, "svc-1", ActorScope.Platform(),
            Actor.Create(ActorType.AuthenticatedUnknown, "sub-9", ActorScope.ForTenant(Acme)).Value).Value,
        AuditOutcome.Success());

    private sealed record StoredRow(
        Guid Id, long? Sequence, byte[]? Payload, byte[] LeafHash,
        string? EventType, string? Actor, string? ActorId, string[]? Targets, string? Outcome);

    [SkippableFact]
    public async Task Migration_BackfillsSearchColumnsFromStoredPayloads_AndVerificationStillPasses()
    {
        Skip.If(postgres.SkipReason is not null, postgres.SkipReason);
        var connectionString = await postgres.CreateDatabaseAsync();

        await using (var db = PostgresFixture.CreateContext(connectionString))
            await db.GetService<IMigrator>().MigrateAsync(MigrationBeforeSearchColumns);

        var tenantEvent = TenantEvent();
        var platformEvent = PlatformEvent();
        var purgedId = new Guid(Interlocked.Increment(ref _sequence), 0, 0, new byte[8]);
        var corruptId = new Guid(Interlocked.Increment(ref _sequence), 0, 0, new byte[8]);
        var corruptPayload = "not json at all"u8.ToArray();
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await InsertLegacyRowAsync(connection, tenantEvent.Id, Acme, CanonicalAuditEventSerializer.Serialize(tenantEvent));
            await InsertLegacyRowAsync(connection, platformEvent.Id, Acme, CanonicalAuditEventSerializer.Serialize(platformEvent));
            await InsertLegacyRowAsync(connection, purgedId, Acme, payload: null, leafHash: MerkleHash.Leaf([7, 7, 7]));
            await InsertLegacyRowAsync(connection, corruptId, Acme, corruptPayload);
        }
        var before = await ReadRowsAsync(connectionString);

        await using (var db = PostgresFixture.CreateContext(connectionString))
            await db.Database.MigrateAsync();

        var after = await ReadRowsAsync(connectionString);

        foreach (var row in before.Values)
        {
            Assert.Equal(row.Payload, after[row.Id].Payload);
            Assert.Equal(row.LeafHash, after[row.Id].LeafHash);
        }
        AssertSearchColumnsMatchWritePath(after[tenantEvent.Id], tenantEvent);
        AssertSearchColumnsMatchWritePath(after[platformEvent.Id], platformEvent);
        AssertSearchColumnsEmpty(after[purgedId]);
        AssertSearchColumnsEmpty(after[corruptId]);

        await SealEverythingAsync(connectionString);
        await AssertVerifiesAsync(connectionString, expectedEvents: 4);
    }

    [SkippableFact]
    public async Task Backfill_OverAnAlreadySealedChain_LeavesVerificationPassing()
    {
        Skip.If(postgres.SkipReason is not null, postgres.SkipReason);
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var db = PostgresFixture.CreateContext(connectionString))
            await db.Database.MigrateAsync();

        var recorder = new AuditRecorder(new NpgsqlAuditDbContextFactory(connectionString), NullLogger<AuditRecorder>.Instance);
        var events = new[] { TenantEvent(), PlatformEvent(), TenantEvent() };
        foreach (var auditEvent in events) await recorder.RecordAsync(auditEvent, Acme);

        await SealEverythingAsync(connectionString);
        await AssertVerifiesAsync(connectionString, expectedEvents: 3);
        var sealedRows = await ReadRowsAsync(connectionString);

        // Put the rows back in the state the migration finds on an existing database: sealed, with
        // empty search columns. The append-only trigger rightly forbids this, so it's bypassed here.
        await ExecuteAsync(connectionString, """
            SET session_replication_role = replica;
            UPDATE audit.events SET "EventType" = NULL, "Actor" = NULL, "ActorId" = NULL, "Targets" = NULL, "Outcome" = NULL;
            """);
        Assert.All((await ReadRowsAsync(connectionString)).Values, AssertSearchColumnsEmpty);

        await ExecuteAsync(connectionString, AddEventSearchColumns.BackfillSql);

        var backfilled = await ReadRowsAsync(connectionString);
        foreach (var auditEvent in events)
        {
            var row = backfilled[auditEvent.Id];
            AssertSearchColumnsMatchWritePath(row, auditEvent);
            Assert.Equal(sealedRows[auditEvent.Id].Sequence, row.Sequence);
            Assert.Equal(sealedRows[auditEvent.Id].Payload, row.Payload);
            Assert.Equal(sealedRows[auditEvent.Id].LeafHash, row.LeafHash);
        }
        await AssertVerifiesAsync(connectionString, expectedEvents: 3);
    }

    [SkippableFact]
    public async Task AppendOnlyTrigger_RejectsChangingASearchColumnOnceSet()
    {
        Skip.If(postgres.SkipReason is not null, postgres.SkipReason);
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var db = PostgresFixture.CreateContext(connectionString))
            await db.Database.MigrateAsync();
        var recorder = new AuditRecorder(new NpgsqlAuditDbContextFactory(connectionString), NullLogger<AuditRecorder>.Instance);
        await recorder.RecordAsync(TenantEvent(), Acme);

        foreach (var assignment in new[]
                 {
                     "\"EventType\" = 'admin.entity.delete'",
                     "\"Actor\" = NULL",
                     "\"ActorId\" = 'someone-else'",
                     "\"Targets\" = ARRAY['user:other']",
                     "\"Outcome\" = 'Success'"
                 })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(
                () => ExecuteAsync(connectionString, $"UPDATE audit.events SET {assignment}"));
            Assert.Contains("search columns", error.MessageText);
        }
    }

    private static void AssertSearchColumnsMatchWritePath(StoredRow row, AuditEvent auditEvent)
    {
        var expected = AuditEventRecordFactory.Create(auditEvent, Acme);
        Assert.Equal(expected.EventType, row.EventType);
        Assert.Equal(expected.Actor, row.Actor);
        Assert.Equal(expected.ActorId, row.ActorId);
        Assert.Equal(expected.Targets, row.Targets);
        Assert.Equal(expected.Outcome, row.Outcome);
    }

    private static void AssertSearchColumnsEmpty(StoredRow row)
    {
        Assert.Null(row.EventType);
        Assert.Null(row.Actor);
        Assert.Null(row.ActorId);
        Assert.Null(row.Targets);
        Assert.Null(row.Outcome);
    }

    private static async Task SealEverythingAsync(string connectionString)
    {
        var factory = new NpgsqlAuditDbContextFactory(connectionString);
        var sealer = new AuditSealerService(
            factory,
            new PgSealableEventReader(),
            new PgChannelAdvisoryLock(),
            new Es256CheckpointSigner(new EphemeralDataProtectionProvider(), factory),
            [],
            Options.Create(new AuditSealerOptions
            {
                MaxSegmentSize = 2,
                MaxSegmentAgeSeconds = 0,
                MaxSegmentsPerCheckpoint = 1,
                MaxCheckpointIntervalSeconds = 0
            }),
            NullLogger<AuditSealerService>.Instance);

        // The sealer only takes rows below the cluster-wide snapshot watermark (I4), so a transaction
        // open anywhere on the server (another test assembly sharing it) holds sealing back for a while.
        for (var pass = 0; pass < 150; pass++)
        {
            await sealer.RunPassAsync(CancellationToken.None);
            await using var db = factory.CreateDbContext();
            if (!await db.AuditEvents.AnyAsync(e => e.Sequence == null)) return;
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
        Assert.Fail("The sealer left events unsealed.");
    }

    private static async Task AssertVerifiesAsync(string connectionString, int expectedEvents)
    {
        var repository = new AuditProofRepository(new NpgsqlAuditDbContextFactory(connectionString));
        var report = (await new VerifyHandler(repository).Handle(
            new VerifyQuery(AuditChannel.AdminConfig, Acme), CancellationToken.None)).Value;

        Assert.True(report.Verified);
        Assert.NotEmpty(report.Checkpoints);
        Assert.Equal(expectedEvents, report.Segments.Sum(s => s.SequenceEnd - s.SequenceStart + 1));
    }

    private static async Task InsertLegacyRowAsync(
        NpgsqlConnection connection, Guid id, Guid tenantId, byte[]? payload, byte[]? leafHash = null)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO audit.events ("Id", "Channel", "Sequence", "TenantId", "OccurredAt", "Payload", "LeafHash")
            VALUES (@id, 'AdminConfig', NULL, @tenantId, now() - interval '10 minutes', @payload, @leafHash)
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("tenantId", tenantId);
        command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlTypes.NpgsqlDbType.Bytea) { Value = (object?)payload ?? DBNull.Value });
        command.Parameters.AddWithValue("leafHash", leafHash ?? MerkleHash.Leaf(payload!));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Dictionary<Guid, StoredRow>> ReadRowsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var hasSearchColumns = await HasColumnAsync(connection, "EventType");
        await using var command = new NpgsqlCommand(
            hasSearchColumns
                ? """SELECT "Id", "Sequence", "Payload", "LeafHash", "EventType", "Actor", "ActorId", "Targets", "Outcome" FROM audit.events"""
                : """SELECT "Id", "Sequence", "Payload", "LeafHash" FROM audit.events""",
            connection);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new Dictionary<Guid, StoredRow>();
        while (await reader.ReadAsync())
        {
            string? Text(int ordinal) => hasSearchColumns && !reader.IsDBNull(ordinal) ? reader.GetString(ordinal) : null;
            rows[reader.GetGuid(0)] = new StoredRow(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetInt64(1),
                reader.IsDBNull(2) ? null : (byte[])reader[2],
                (byte[])reader[3],
                Text(4), Text(5), Text(6),
                hasSearchColumns && !reader.IsDBNull(7) ? (string[])reader[7] : null,
                Text(8));
        }
        return rows;
    }

    private static async Task<bool> HasColumnAsync(NpgsqlConnection connection, string column)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_schema = 'audit' AND table_name = 'events' AND column_name = @column)
            """, connection);
        command.Parameters.AddWithValue("column", column);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
