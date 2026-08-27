using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WardenId.Audit.Infrastructure.Retention;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>
/// Exercises AuditRetentionService's purge-eligibility orchestration against InMemory —
/// RetentionPolicy itself (the pure age/export decision) is unit tested directly in
/// WardenId.Audit.Domain.Tests.
/// </summary>
public class AuditRetentionServiceTests
{
    private sealed class InMemoryAuditDbContextFactory(string databaseName) : IAuditRetentionDbContextFactory
    {
        public AuditDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(databaseName).Options);
    }

    private static AuditRetentionService CreateSut(InMemoryAuditDbContextFactory factory, AuditRetentionOptions? options = null) =>
        new(factory, Options.Create(options ?? new AuditRetentionOptions()), NullLogger<AuditRetentionService>.Instance);

    private static AuditEventRecord OldEvent(int i, AuditChannel channel, Guid? tenantId, long sequence, DateTimeOffset occurredAt) => new()
    {
        Id = new Guid(i, 0, 0, new byte[8]),
        Channel = channel.ToString(),
        TenantId = tenantId,
        Sequence = sequence,
        OccurredAt = occurredAt,
        Payload = [(byte)i, 1, 2, 3],
        LeafHash = [(byte)(i + 1)]
    };

    private static AuditSegmentRecord Segment(AuditChannel channel, Guid? tenantId, long start, long end, DateTimeOffset? exportedAt) => new()
    {
        Id = Guid.CreateVersion7(),
        Channel = channel.ToString(),
        TenantId = tenantId,
        SequenceStart = start,
        SequenceEnd = end,
        MerkleRoot = [9, 9],
        HeaderHash = [8, 8],
        SealedAt = DateTimeOffset.UtcNow,
        ExportedAt = exportedAt
    };

    [Fact]
    public async Task RunPassAsync_ExportedAndPastRetention_NullsPayload_KeepsLeafHash()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_ExportedAndPastRetention_NullsPayload_KeepsLeafHash));
        var oldEnough = DateTimeOffset.UtcNow - TimeSpan.FromDays(2);
        await using (var seed = factory.CreateDbContext())
        {
            seed.AuditEvents.Add(OldEvent(1, AuditChannel.TokenOps, null, 1, oldEnough));
            seed.AuditSegments.Add(Segment(AuditChannel.TokenOps, null, 1, 1, exportedAt: DateTimeOffset.UtcNow));
            await seed.SaveChangesAsync();
        }

        await CreateSut(factory, new AuditRetentionOptions { TokenOpsDays = 1 }).RunPassAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        var row = await db.AuditEvents.SingleAsync();
        Assert.Null(row.Payload);
        Assert.Equal([2], row.LeafHash);
    }

    [Fact]
    public async Task RunPassAsync_NotExported_LeavesPayload_EvenIfVeryOld()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_NotExported_LeavesPayload_EvenIfVeryOld));
        var veryOld = DateTimeOffset.UtcNow - TimeSpan.FromDays(3650);
        await using (var seed = factory.CreateDbContext())
        {
            seed.AuditEvents.Add(OldEvent(1, AuditChannel.TokenOps, null, 1, veryOld));
            seed.AuditSegments.Add(Segment(AuditChannel.TokenOps, null, 1, 1, exportedAt: null));
            await seed.SaveChangesAsync();
        }

        await CreateSut(factory, new AuditRetentionOptions { TokenOpsDays = 1 }).RunPassAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        Assert.NotNull(await db.AuditEvents.Select(e => e.Payload).SingleAsync());
    }

    [Fact]
    public async Task RunPassAsync_ExportedButWithinRetentionWindow_LeavesPayload()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_ExportedButWithinRetentionWindow_LeavesPayload));
        var recent = DateTimeOffset.UtcNow - TimeSpan.FromHours(1);
        await using (var seed = factory.CreateDbContext())
        {
            seed.AuditEvents.Add(OldEvent(1, AuditChannel.AdminConfig, null, 1, recent));
            seed.AuditSegments.Add(Segment(AuditChannel.AdminConfig, null, 1, 1, exportedAt: DateTimeOffset.UtcNow));
            await seed.SaveChangesAsync();
        }

        await CreateSut(factory, new AuditRetentionOptions { AdminConfigDays = 2555 }).RunPassAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        Assert.NotNull(await db.AuditEvents.Select(e => e.Payload).SingleAsync());
    }

    [Fact]
    public async Task RunPassAsync_UnsealedRows_NeverPurged()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_UnsealedRows_NeverPurged));
        var veryOld = DateTimeOffset.UtcNow - TimeSpan.FromDays(3650);
        await using (var seed = factory.CreateDbContext())
        {
            // Sequence == null: never sealed, so no segment can cover it — must stay untouched.
            seed.AuditEvents.Add(new AuditEventRecord
            {
                Id = Guid.NewGuid(),
                Channel = AuditChannel.TokenOps.ToString(),
                TenantId = null,
                Sequence = null,
                OccurredAt = veryOld,
                Payload = [1, 2, 3],
                LeafHash = [1]
            });
            await seed.SaveChangesAsync();
        }

        await CreateSut(factory, new AuditRetentionOptions { TokenOpsDays = 1 }).RunPassAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        Assert.NotNull(await db.AuditEvents.Select(e => e.Payload).SingleAsync());
    }

    [Fact]
    public async Task RunPassAsync_AlreadyPurgedRow_IsIdempotent()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_AlreadyPurgedRow_IsIdempotent));
        var oldEnough = DateTimeOffset.UtcNow - TimeSpan.FromDays(2);
        await using (var seed = factory.CreateDbContext())
        {
            seed.AuditEvents.Add(new AuditEventRecord
            {
                Id = Guid.NewGuid(),
                Channel = AuditChannel.TokenOps.ToString(),
                TenantId = null,
                Sequence = 1,
                OccurredAt = oldEnough,
                Payload = null,
                LeafHash = [1]
            });
            seed.AuditSegments.Add(Segment(AuditChannel.TokenOps, null, 1, 1, exportedAt: DateTimeOffset.UtcNow));
            await seed.SaveChangesAsync();
        }

        var sut = CreateSut(factory, new AuditRetentionOptions { TokenOpsDays = 1 });
        await sut.RunPassAsync(CancellationToken.None);
        await sut.RunPassAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        Assert.Null(await db.AuditEvents.Select(e => e.Payload).SingleAsync());
    }
}
