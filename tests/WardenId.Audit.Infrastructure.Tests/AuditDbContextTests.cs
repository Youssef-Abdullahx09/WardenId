using Microsoft.EntityFrameworkCore;

namespace WardenId.Audit.Infrastructure.Tests;

public class AuditDbContextTests
{
    private static AuditDbContext CreateInMemory(string databaseName) =>
        new(new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(databaseName).Options);

    [Fact]
    public async Task Insert_ThenReadBack_RoundTripsAllColumns()
    {
        await using var db = CreateInMemory(nameof(Insert_ThenReadBack_RoundTripsAllColumns));

        var record = new AuditEventRecord
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Channel = AuditChannel.AdminConfig.ToString(),
            Sequence = null,
            TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            OccurredAt = DateTimeOffset.Parse("2026-08-21T00:00:00Z"),
            Payload = [1, 2, 3],
            LeafHash = [4, 5, 6]
        };

        db.AuditEvents.Add(record);
        await db.SaveChangesAsync(CancellationToken.None);

        await using var reader = CreateInMemory(nameof(Insert_ThenReadBack_RoundTripsAllColumns));
        var reloaded = await reader.AuditEvents.SingleAsync();

        Assert.Equal(record.Id, reloaded.Id);
        Assert.Equal(record.Channel, reloaded.Channel);
        Assert.Null(reloaded.Sequence);
        Assert.Equal(record.TenantId, reloaded.TenantId);
        Assert.Equal(record.OccurredAt, reloaded.OccurredAt);
        Assert.Equal(record.Payload, reloaded.Payload);
        Assert.Equal(record.LeafHash, reloaded.LeafHash);
    }

    [Fact]
    public async Task TwoRows_WithSameId_DifferentChannel_BothPersist()
    {
        // Proves the composite (Id, Channel) key is what's actually enforced, not Id alone —
        // required by Postgres declarative partitioning (see the InitialCreate migration).
        await using var db = CreateInMemory(nameof(TwoRows_WithSameId_DifferentChannel_BothPersist));
        var sameId = Guid.NewGuid();

        db.AuditEvents.Add(new AuditEventRecord
        {
            Id = sameId, Channel = AuditChannel.AuthnInteractive.ToString(),
            OccurredAt = DateTimeOffset.UtcNow, LeafHash = [1]
        });
        db.AuditEvents.Add(new AuditEventRecord
        {
            Id = sameId, Channel = AuditChannel.Platform.ToString(),
            OccurredAt = DateTimeOffset.UtcNow, LeafHash = [2]
        });
        await db.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(2, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task PlainSaveChangesAsync_DoesNotThrow_EvenThoughRecordIsNotAnEntity()
    {
        // AuditEventRecord isn't an Entity/AggregateRoot (spec §11), so the plain
        // DbContext.SaveChangesAsync(ct) is the right overload — no userId to stamp.
        await using var db = CreateInMemory(nameof(PlainSaveChangesAsync_DoesNotThrow_EvenThoughRecordIsNotAnEntity));
        db.AuditEvents.Add(new AuditEventRecord
        {
            Id = Guid.NewGuid(), Channel = AuditChannel.AdminConfig.ToString(), OccurredAt = DateTimeOffset.UtcNow, LeafHash = [1]
        });

        var rows = await db.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(1, rows);
    }
}
