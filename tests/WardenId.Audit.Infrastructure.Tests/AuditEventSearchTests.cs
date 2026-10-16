using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Application.Abstractions;

namespace WardenId.Audit.Infrastructure.Tests;

public class AuditEventSearchTests
{
    private sealed class InMemoryAuditDbContextFactory(string databaseName) : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(databaseName).Options);
    }

    private static readonly Guid Acme = Guid.NewGuid();
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-16T08:00:00Z");

    private static async Task<AuditEventSearch> SeedAsync(string name)
    {
        var factory = new InMemoryAuditDbContextFactory(name);
        await using var db = factory.CreateDbContext();
        for (var minute = 0; minute < 5; minute++)
        {
            db.AuditEvents.Add(new AuditEventRecord
            {
                Id = Guid.NewGuid(),
                Channel = (minute % 2 == 0 ? AuditChannel.AdminConfig : AuditChannel.TokenOps).ToString(),
                TenantId = minute < 3 ? Acme : null,
                Sequence = minute,
                OccurredAt = Start.AddMinutes(minute),
                Payload = [1],
                LeafHash = [1],
                EventType = minute < 2 ? "admin.tenant.suspend" : "admin.entity.update",
                Actor = minute == 4 ? "platform:system:sealer" : $"tenant:{Acme}:adminuser:admin-{minute % 2}",
                ActorId = minute == 4 ? "sealer" : $"admin-{minute % 2}",
                Targets = minute == 3 ? ["tenant:t-1", "user:u-1"] : [$"tenant:t-{minute}"],
                Outcome = minute == 2 ? nameof(AuditResult.Failure) : nameof(AuditResult.Success),
            });
        }
        await db.SaveChangesAsync();
        return new AuditEventSearch(factory);
    }

    private static readonly AuditEventFilter All = new(null, null, null, null);

    [Fact]
    public async Task SearchAsync_PagesNewestFirst()
    {
        var search = await SeedAsync(nameof(SearchAsync_PagesNewestFirst));

        var (total, rows) = await search.SearchAsync(All, pageNumber: 2, pageSize: 2);

        Assert.Equal(5, total);
        Assert.Equal([2L, 1L], rows.Select(r => r.Sequence!.Value));
    }

    [Fact]
    public async Task SearchAsync_FiltersByChannelTenantAndTime()
    {
        var search = await SeedAsync(nameof(SearchAsync_FiltersByChannelTenantAndTime));

        var (byChannel, _) = await search.SearchAsync(All with { Channel = AuditChannel.TokenOps }, 1, 25);
        var (byTenant, _) = await search.SearchAsync(All with { TenantId = Acme }, 1, 25);
        var (_, inRange) = await search.SearchAsync(All with { From = Start.AddMinutes(1), To = Start.AddMinutes(3) }, 1, 25);

        Assert.Equal(2, byChannel);
        Assert.Equal(3, byTenant);
        Assert.Equal([3L, 2L, 1L], inRange.Select(r => r.Sequence!.Value));
        Assert.All(inRange, r => Assert.NotNull(r.Payload));
    }

    [Fact]
    public async Task SearchAsync_FiltersByEventTypeActorTargetAndOutcome()
    {
        var search = await SeedAsync(nameof(SearchAsync_FiltersByEventTypeActorTargetAndOutcome));

        var (_, byType) = await search.SearchAsync(All with { EventType = "admin.tenant.suspend" }, 1, 25);
        var (_, byFullActor) = await search.SearchAsync(All with { Actor = $"tenant:{Acme}:adminuser:admin-1" }, 1, 25);
        var (_, byActorId) = await search.SearchAsync(All with { Actor = "admin-0" }, 1, 25);
        var (_, byTarget) = await search.SearchAsync(All with { Target = "tenant:t-1" }, 1, 25);
        var (_, bySecondTarget) = await search.SearchAsync(All with { Target = "user:u-1" }, 1, 25);
        var (_, byOutcome) = await search.SearchAsync(All with { Outcome = AuditResult.Failure }, 1, 25);
        var (combined, _) = await search.SearchAsync(
            All with { EventType = "admin.entity.update", Outcome = AuditResult.Success }, 1, 25);

        Assert.Equal([1L, 0L], byType.Select(r => r.Sequence!.Value));
        Assert.Equal([3L, 1L], byFullActor.Select(r => r.Sequence!.Value));
        Assert.Equal([2L, 0L], byActorId.Select(r => r.Sequence!.Value));
        Assert.Equal([3L, 1L], byTarget.Select(r => r.Sequence!.Value));
        Assert.Equal([3L], bySecondTarget.Select(r => r.Sequence!.Value));
        Assert.Equal([2L], byOutcome.Select(r => r.Sequence!.Value));
        Assert.Equal(2, combined);
    }

    [Fact]
    public async Task SearchAsync_ReturnsTheSearchColumns()
    {
        var search = await SeedAsync(nameof(SearchAsync_ReturnsTheSearchColumns));

        var (_, rows) = await search.SearchAsync(All with { Target = "user:u-1" }, 1, 25);

        var row = Assert.Single(rows);
        Assert.Equal("admin.entity.update", row.EventType);
        Assert.Equal($"tenant:{Acme}:adminuser:admin-1", row.Actor);
        Assert.Equal(["tenant:t-1", "user:u-1"], row.Targets);
        Assert.Equal(AuditResult.Success, row.Outcome);
    }

    [Fact]
    public async Task FindAsync_ReturnsTheRowOrNull()
    {
        var search = await SeedAsync(nameof(FindAsync_ReturnsTheRowOrNull));
        var (_, rows) = await search.SearchAsync(All, 1, 1);

        var found = await search.FindAsync(rows[0].Id);
        var missing = await search.FindAsync(Guid.NewGuid());

        Assert.NotNull(found);
        Assert.Equal(rows[0].Id, found.Id);
        Assert.Equal(rows[0].Sequence, found.Sequence);
        Assert.Equal(rows[0].Targets, found.Targets);
        Assert.Null(missing);
    }
}
