using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WardenId.Audit.Application.Abstractions;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>The search filters as Npgsql translates them, against the migrated schema.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AuditEventSearchPostgresTests(PostgresFixture postgres)
{
    private static readonly Guid Acme = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static AuditEvent MakeEvent(AuditEventType type, string actorId, AuditOutcome outcome, params AuditTarget[] targets) =>
        AuditEvent.Create(
            Guid.CreateVersion7(), AuditChannel.AdminDirectory, type, 1, DateTimeOffset.UtcNow, AuditSeverity.Info,
            Actor.Create(ActorType.AdminUser, actorId, ActorScope.ForTenant(Acme)).Value,
            outcome, ClientContext.Unknown(), AuthContext.None(), Correlation.None(), targets).Value;

    [SkippableFact]
    public async Task SearchAsync_FiltersOnTheSearchColumns()
    {
        Skip.If(postgres.SkipReason is not null, postgres.SkipReason);
        var connectionString = await postgres.CreateDatabaseAsync();
        var factory = new NpgsqlAuditDbContextFactory(connectionString);
        await using (var db = factory.CreateDbContext())
            await db.Database.MigrateAsync();

        var recorder = new AuditRecorder(factory, NullLogger<AuditRecorder>.Instance);
        var suspend = MakeEvent(AuditEventType.AdminTenantSuspend, "admin-1", AuditOutcome.Failure(AuditOutcomeReason.PolicyDenied),
            AuditTarget.Create(AuditTargetType.Tenant, "t-1").Value, AuditTarget.Create(AuditTargetType.User, "u-1").Value);
        var update = MakeEvent(AuditEventType.AdminEntityUpdate, "admin-2", AuditOutcome.Success(),
            AuditTarget.Create(AuditTargetType.User, "u-1").Value);
        var create = MakeEvent(AuditEventType.AdminEntityCreate, "admin-2", AuditOutcome.Success());
        foreach (var auditEvent in new[] { suspend, update, create }) await recorder.RecordAsync(auditEvent, Acme);

        var search = new AuditEventSearch(factory);
        var all = new AuditEventFilter(null, null, null, null);

        async Task<Guid[]> Ids(AuditEventFilter filter) =>
            (await search.SearchAsync(filter, 1, 25)).Rows.Select(r => r.Id).Order().ToArray();
        Guid[] Expect(params AuditEvent[] events) => events.Select(e => e.Id).Order().ToArray();

        Assert.Equal(Expect(suspend), await Ids(all with { EventType = "admin.tenant.suspend" }));
        Assert.Equal(Expect(update, create), await Ids(all with { Actor = "admin-2" }));
        Assert.Equal(Expect(suspend), await Ids(all with { Actor = $"tenant:{Acme}:adminuser:admin-1" }));
        Assert.Equal(Expect(suspend, update), await Ids(all with { Target = "user:u-1" }));
        Assert.Equal(Expect(suspend), await Ids(all with { Target = "tenant:t-1" }));
        Assert.Empty(await Ids(all with { Target = "user:nobody" }));
        Assert.Equal(Expect(update, create), await Ids(all with { Outcome = AuditResult.Success }));
        Assert.Equal(Expect(update), await Ids(all with { Actor = "admin-2", Target = "user:u-1" }));

        var found = await search.FindAsync(suspend.Id);
        Assert.NotNull(found);
        Assert.Equal(["tenant:t-1", "user:u-1"], found.Targets);
        Assert.Equal(AuditResult.Failure, found.Outcome);

        // Array containment (@>) is what the GIN index on Targets serves; "= ANY(...)" would scan.
        await using var context = factory.CreateDbContext();
        var target = "user:u-1";
        var sql = context.AuditEvents.Where(e => e.Targets != null && e.Targets.Contains(target)).ToQueryString();
        Assert.Contains("\"Targets\" @> ARRAY[", sql);
    }
}
