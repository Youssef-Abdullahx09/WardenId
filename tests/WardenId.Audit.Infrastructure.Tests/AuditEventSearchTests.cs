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
}
