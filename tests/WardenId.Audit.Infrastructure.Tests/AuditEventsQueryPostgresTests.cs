using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using WardenId.Audit.Query;
using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Query;
using WardenId.SharedKernel.Query.Contracts;
using GetQuery = WardenId.Audit.Query.Contracts.Features.AuditEvents.Get.Query;
using ListQuery = WardenId.Audit.Query.Contracts.Features.AuditEvents.List.Query;
using ReadAuditChannel = WardenId.Audit.Query.Contracts.Features.AuditEvents.AuditChannel;
using ReadAuditResult = WardenId.Audit.Query.Contracts.Features.AuditEvents.AuditResult;
using SortOption = WardenId.Audit.Query.Contracts.Features.AuditEvents.List.SortOption;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>The Dapper read side (Audit.Query's SQL files) against the migrated audit.events table.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AuditEventsQueryPostgresTests(PostgresFixture postgres)
{
    private static readonly Guid Acme = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-16T08:00:00Z");
    private static readonly ListQuery All = new(1, 25, SortDirection.Desc);

    /// <summary>
    /// Five rows a minute apart; Sequence = minute, so results read as sequences. <paramref name="withUnsealed"/> adds a
    /// sixth, newest row that has no sequence yet.
    /// </summary>
    private async Task<(AuditEventsQuery Query, NpgsqlConnection Connection)?> SeedAsync(bool withUnsealed = false)
    {
        if (postgres.SkipReason is not null) return null;
        var connectionString = await postgres.CreateDatabaseAsync();
        var factory = new NpgsqlAuditDbContextFactory(connectionString);
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();

        for (var minute = 0; minute < 5; minute++)
        {
            db.AuditEvents.Add(new AuditEventRecord
            {
                Id = Guid.CreateVersion7(),
                Channel = (minute % 2 == 0 ? AuditChannel.AdminConfig : AuditChannel.TokenOps).ToString(),
                TenantId = minute < 3 ? Acme : null,
                Sequence = minute,
                OccurredAt = Start.AddMinutes(minute),
                Payload = minute == 4 ? null : [1],
                LeafHash = [1],
                EventType = minute < 2 ? "admin.tenant.suspend" : "admin.entity.update",
                Actor = minute == 4 ? "platform:system:sealer" : $"tenant:{Acme}:adminuser:admin-{minute % 2}",
                ActorId = minute == 4 ? "sealer" : $"admin-{minute % 2}",
                Targets = minute switch { 3 => ["tenant:t-1", "user:u-1"], 4 => null, _ => [$"tenant:t-{minute}"] },
                Outcome = minute == 2 ? nameof(AuditResult.Failure) : nameof(AuditResult.Success),
            });
        }
        if (withUnsealed)
        {
            db.AuditEvents.Add(new AuditEventRecord
            {
                Id = Guid.CreateVersion7(),
                Channel = nameof(AuditChannel.AdminConfig),
                OccurredAt = Start.AddMinutes(10),
                Payload = [1],
                LeafHash = [1],
            });
        }
        await db.SaveChangesAsync();

        var connection = new NpgsqlConnection(connectionString);
        var query = new AuditEventsQuery(
            new ConnectionWrapper(connection),
            new ReadFileService(NullLogger<ReadFileService>.Instance));
        return (query, connection);
    }

    private static async Task<long[]> Sequences(AuditEventsQuery query, ListQuery filter) =>
        (await query.List(filter, CancellationToken.None)).Value.Data.Select(r => r.Sequence!.Value).ToArray();

    [SkippableFact]
    public async Task List_PagesNewestFirst()
    {
        var seeded = await SeedAsync();
        Skip.If(seeded is null, postgres.SkipReason);
        var (query, connection) = seeded.Value;
        await using var _ = connection;

        var result = await query.List(All with { PageNumber = 2, PageSize = 2 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value.RecordsTotal);
        Assert.Equal([2L, 1L], result.Value.Data.Select(r => r.Sequence!.Value));
    }

    [SkippableFact]
    public async Task List_FiltersByChannelTenantAndInclusiveTimeRange()
    {
        var seeded = await SeedAsync();
        Skip.If(seeded is null, postgres.SkipReason);
        var (query, connection) = seeded.Value;
        await using var _ = connection;

        var byChannel = await query.List(All with { Channel = ReadAuditChannel.TokenOps }, CancellationToken.None);
        var byTenant = await query.List(All with { TenantId = Acme }, CancellationToken.None);

        Assert.Equal(2, byChannel.Value.RecordsTotal);
        Assert.Equal(3, byTenant.Value.RecordsTotal);
        Assert.Equal(new long[] { 3L, 2L, 1L }, await Sequences(query, All with { From = Start.AddMinutes(1), To = Start.AddMinutes(3) }));
        // A non-UTC offset names the same instant.
        Assert.Equal(new long[] { 3L, 2L, 1L }, await Sequences(query, All with
        {
            From = Start.AddMinutes(1).ToOffset(TimeSpan.FromHours(2)),
            To = Start.AddMinutes(3).ToOffset(TimeSpan.FromHours(-5))
        }));
    }

    [SkippableFact]
    public async Task List_FiltersByEventTypeActorTargetAndOutcome()
    {
        var seeded = await SeedAsync();
        Skip.If(seeded is null, postgres.SkipReason);
        var (query, connection) = seeded.Value;
        await using var _ = connection;

        Assert.Equal(new long[] { 1L, 0L }, await Sequences(query, All with { EventType = "admin.tenant.suspend" }));
        Assert.Equal(new long[] { 3L, 1L }, await Sequences(query, All with { Actor = $"tenant:{Acme}:adminuser:admin-1" }));
        Assert.Equal(new long[] { 2L, 0L }, await Sequences(query, All with { Actor = "admin-0" }));
        Assert.Equal(new long[] { 3L, 1L }, await Sequences(query, All with { Target = "tenant:t-1" }));
        Assert.Equal(new long[] { 3L }, await Sequences(query, All with { Target = "user:u-1" }));
        Assert.Empty(await Sequences(query, All with { Target = "user:nobody" }));
        Assert.Equal(new long[] { 2L }, await Sequences(query, All with { Outcome = ReadAuditResult.Failure }));
        Assert.Equal(2, (await query.List(
            All with { EventType = "admin.entity.update", Outcome = ReadAuditResult.Success },
            CancellationToken.None)).Value.RecordsTotal);
        Assert.Equal(new long[] { 3L }, await Sequences(query, All with { Actor = "admin-1", Target = "user:u-1" }));
    }

    [SkippableFact]
    public async Task List_TrimsTextFiltersAndTreatsBlankOnesAsAbsent()
    {
        var seeded = await SeedAsync();
        Skip.If(seeded is null, postgres.SkipReason);
        var (query, connection) = seeded.Value;
        await using var _ = connection;

        Assert.Equal(new long[] { 1L, 0L }, await Sequences(query, All with { EventType = " admin.tenant.suspend " }));
        Assert.Equal(new long[] { 3L, 1L }, await Sequences(query, All with { Target = "\ttenant:t-1 " }));
        Assert.Equal(5, (await query.List(All with { EventType = " ", Actor = "", Target = "  " }, CancellationToken.None))
            .Value.RecordsTotal);
    }

    [SkippableTheory]
    [InlineData(SortOption.OccurredAt, SortDirection.Desc, new[] { 4L, 3L, 2L, 1L, 0L })]
    [InlineData(SortOption.OccurredAt, SortDirection.Asc, new[] { 0L, 1L, 2L, 3L, 4L })]
    [InlineData(SortOption.Sequence, SortDirection.Asc, new[] { 0L, 1L, 2L, 3L, 4L })]
    [InlineData(SortOption.Sequence, SortDirection.Desc, new[] { 4L, 3L, 2L, 1L, 0L })]
    public async Task List_OrdersBySortOption(SortOption sortOption, SortDirection sortDirection, long[] expectedSequences)
    {
        var seeded = await SeedAsync();
        Skip.If(seeded is null, postgres.SkipReason);
        var (query, connection) = seeded.Value;
        await using var _ = connection;

        Assert.Equal(expectedSequences, await Sequences(query, new ListQuery(1, 25, sortDirection) { SortOption = sortOption }));
    }

    [SkippableTheory]
    [InlineData(SortDirection.Asc)]
    [InlineData(SortDirection.Desc)]
    public async Task List_SortsUnsealedEventsLast_BySequence(SortDirection sortDirection)
    {
        var seeded = await SeedAsync(withUnsealed: true);
        Skip.If(seeded is null, postgres.SkipReason);
        var (query, connection) = seeded.Value;
        await using var _ = connection;

        var rows = (await query.List(new ListQuery(1, 25, sortDirection) { SortOption = SortOption.Sequence }, CancellationToken.None))
            .Value.Data.ToArray();

        Assert.Equal(6, rows.Length);
        Assert.Null(rows[^1].Sequence);
        Assert.All(rows[..^1], r => Assert.NotNull(r.Sequence));
    }

    [SkippableFact]
    public async Task List_ReturnsTheColumns()
    {
        var seeded = await SeedAsync();
        Skip.If(seeded is null, postgres.SkipReason);
        var (query, connection) = seeded.Value;
        await using var _ = connection;

        var rows = (await query.List(All, CancellationToken.None)).Value.Data.ToArray();

        var row = rows.Single(r => r.Sequence == 3);
        Assert.Equal(ReadAuditChannel.TokenOps, row.Channel);
        Assert.Null(row.TenantId);
        Assert.Equal(Start.AddMinutes(3).UtcDateTime, row.OccurredAt);
        Assert.Equal([1], row.Payload);
        Assert.Equal("admin.entity.update", row.EventType);
        Assert.Equal($"tenant:{Acme}:adminuser:admin-1", row.Actor);
        Assert.Equal(["tenant:t-1", "user:u-1"], row.Targets);
        Assert.Equal(ReadAuditResult.Success, row.Outcome);

        var purged = rows.Single(r => r.Sequence == 4);
        Assert.Null(purged.Payload);
        Assert.Empty(purged.Targets);
    }

    [SkippableFact]
    public async Task Get_ReturnsTheRowOrNull()
    {
        var seeded = await SeedAsync();
        Skip.If(seeded is null, postgres.SkipReason);
        var (query, connection) = seeded.Value;
        await using var _ = connection;
        var first = (await query.List(All with { PageSize = 1 }, CancellationToken.None)).Value.Data.Single();

        var found = await query.Get(new GetQuery(first.Id), CancellationToken.None);
        var missing = await query.Get(new GetQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(found.IsSuccess);
        Assert.NotNull(found.Value);
        Assert.Equal(first.Id, found.Value.Id);
        Assert.Equal(first.Sequence, found.Value.Sequence);
        Assert.Equal(first.Channel.ToString(), found.Value.Channel);
        Assert.Equal(first.Targets, found.Value.Targets);
        Assert.True(missing.IsSuccess);
        Assert.Null(missing.Value);
    }

    [SkippableFact]
    public async Task List_ReadsWhatTheRecorderWrites()
    {
        Skip.If(postgres.SkipReason is not null, postgres.SkipReason);
        var connectionString = await postgres.CreateDatabaseAsync();
        var factory = new NpgsqlAuditDbContextFactory(connectionString);
        await using (var db = factory.CreateDbContext())
            await db.Database.MigrateAsync();

        var auditEvent = AuditEvent.Create(
            Guid.CreateVersion7(), AuditChannel.AdminDirectory, AuditEventType.AdminTenantSuspend, 1, DateTimeOffset.UtcNow,
            AuditSeverity.Info, Actor.Create(ActorType.AdminUser, "admin-1", ActorScope.ForTenant(Acme)).Value,
            AuditOutcome.Failure(AuditOutcomeReason.PolicyDenied), ClientContext.Unknown(), AuthContext.None(), Correlation.None(),
            [AuditTarget.Create(AuditTargetType.Tenant, "t-1").Value, AuditTarget.Create(AuditTargetType.User, "u-1").Value]).Value;
        await new AuditRecorder(factory, NullLogger<AuditRecorder>.Instance).RecordAsync(auditEvent, Acme);

        await using var connection = new NpgsqlConnection(connectionString);
        var query = new AuditEventsQuery(new ConnectionWrapper(connection), new ReadFileService(NullLogger<ReadFileService>.Instance));

        var row = Assert.Single((await query.List(All with { Actor = "admin-1", Target = "user:u-1" }, CancellationToken.None)).Value.Data);
        Assert.Equal(auditEvent.Id, row.Id);
        Assert.Equal(["tenant:t-1", "user:u-1"], row.Targets);
        Assert.Equal(ReadAuditResult.Failure, row.Outcome);
        Assert.Equal(CanonicalAuditEventSerializer.Serialize(auditEvent), row.Payload);
    }
}
