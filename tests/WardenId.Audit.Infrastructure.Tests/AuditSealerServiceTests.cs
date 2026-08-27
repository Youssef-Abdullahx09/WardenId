using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WardenId.Audit.Infrastructure.Sealing;
using WardenId.Audit.Infrastructure.Tests.Fakes;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>
/// Exercises AuditSealerService's dense-sequencing/chaining/policy orchestration against
/// InMemory + fakes for ISealableEventReader/IChannelAdvisoryLock/IAuditCheckpointSigner —
/// the real Postgres-only pieces (xmin watermark filtering, actual advisory locking) have no
/// test here; see PgSealableEventReader/PgChannelAdvisoryLock's own doc comments.
/// </summary>
public class AuditSealerServiceTests
{
    private sealed class InMemoryAuditDbContextFactory(string databaseName) : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(databaseName).Options);
    }

    // .NET's default Guid.CompareTo compares the leading Int32 field first — building every
    // test Guid with a distinct leading int and identical trailing bytes makes OrderBy(e =>
    // e.Id) fully deterministic, standing in for Guid v7's real time-ordering without any
    // wall-clock flakiness.
    private static Guid SequentialGuid(int i) => new(i, 0, 0, new byte[8]);

    private static AuditSealerService CreateSut(
        InMemoryAuditDbContextFactory factory,
        FakeChannelAdvisoryLock? advisoryLock = null,
        FakeAuditCheckpointSigner? signer = null,
        AuditSealerOptions? sealerOptions = null,
        IEnumerable<WardenId.Audit.Application.Abstractions.IAuditCheckpointWitness>? witnesses = null) =>
        new(
            factory,
            new FakeSealableEventReader(),
            advisoryLock ?? new FakeChannelAdvisoryLock(),
            signer ?? new FakeAuditCheckpointSigner(),
            witnesses ?? [],
            Options.Create(sealerOptions ?? new AuditSealerOptions()),
            NullLogger<AuditSealerService>.Instance);

    private static void AddPendingEvents(AuditDbContext db, AuditChannel channel, Guid? tenantId, int count, int startAt = 0)
    {
        for (var i = 0; i < count; i++)
        {
            db.AuditEvents.Add(new AuditEventRecord
            {
                Id = SequentialGuid(startAt + i),
                Channel = channel.ToString(),
                TenantId = tenantId,
                Sequence = null,
                OccurredAt = DateTimeOffset.UtcNow,
                LeafHash = [(byte)(startAt + i + 1)]
            });
        }
        db.SaveChanges();
    }

    [Fact]
    public async Task RunPassAsync_SealsPendingRows_AssignsDenseSequenceStartingAtOne()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_SealsPendingRows_AssignsDenseSequenceStartingAtOne));
        await using (var seed = factory.CreateDbContext())
            AddPendingEvents(seed, AuditChannel.AdminConfig, tenantId: null, count: 3);

        var sut = CreateSut(factory, sealerOptions: new AuditSealerOptions { MaxSegmentSize = 3 });
        await sut.RunPassAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        var sequences = await db.AuditEvents.OrderBy(e => e.Id).Select(e => e.Sequence).ToListAsync();
        Assert.Equal([1L, 2L, 3L], sequences);

        var segment = await db.AuditSegments.SingleAsync();
        Assert.Equal(1, segment.SequenceStart);
        Assert.Equal(3, segment.SequenceEnd);
        Assert.Null(segment.PreviousHeaderHash);
    }

    [Fact]
    public async Task RunPassAsync_SecondSegment_ChainsFromFirst_AndContinuesSequence()
    {
        var dbName = nameof(RunPassAsync_SecondSegment_ChainsFromFirst_AndContinuesSequence);
        var factory = new InMemoryAuditDbContextFactory(dbName);
        await using (var seed = factory.CreateDbContext())
            AddPendingEvents(seed, AuditChannel.AdminConfig, tenantId: null, count: 2);

        var options = new AuditSealerOptions { MaxSegmentSize = 2 };
        await CreateSut(factory, sealerOptions: options).RunPassAsync(CancellationToken.None);

        await using (var seed = factory.CreateDbContext())
            AddPendingEvents(seed, AuditChannel.AdminConfig, tenantId: null, count: 2, startAt: 2);

        await CreateSut(factory, sealerOptions: options).RunPassAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        var segments = await db.AuditSegments.OrderBy(s => s.SequenceStart).ToListAsync();
        Assert.Equal(2, segments.Count);
        Assert.Equal(3, segments[1].SequenceStart);
        Assert.Equal(4, segments[1].SequenceEnd);
        Assert.Equal(segments[0].HeaderHash, segments[1].PreviousHeaderHash);
    }

    [Fact]
    public async Task RunPassAsync_DoesNotSeal_WhenBelowBothSizeAndAgeThresholds()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_DoesNotSeal_WhenBelowBothSizeAndAgeThresholds));
        await using (var seed = factory.CreateDbContext())
            AddPendingEvents(seed, AuditChannel.AdminConfig, tenantId: null, count: 1);

        var options = new AuditSealerOptions { MaxSegmentSize = 100, MaxSegmentAgeSeconds = 3600 };
        await CreateSut(factory, sealerOptions: options).RunPassAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        Assert.False(await db.AuditSegments.AnyAsync());
        Assert.Null(await db.AuditEvents.Select(e => e.Sequence).SingleAsync());
    }

    [Fact]
    public async Task RunPassAsync_SkipsScope_WhenAdvisoryLockNotAcquired()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_SkipsScope_WhenAdvisoryLockNotAcquired));
        await using (var seed = factory.CreateDbContext())
            AddPendingEvents(seed, AuditChannel.AdminConfig, tenantId: null, count: 5);

        var options = new AuditSealerOptions { MaxSegmentSize = 5 };
        await CreateSut(factory, new FakeChannelAdvisoryLock(alwaysDenies: true), sealerOptions: options)
            .RunPassAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        Assert.False(await db.AuditSegments.AnyAsync());
        Assert.All(await db.AuditEvents.Select(e => e.Sequence).ToListAsync(), Assert.Null);
    }

    [Fact]
    public async Task RunPassAsync_DifferentTenants_SameChannel_SealIndependently()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_DifferentTenants_SameChannel_SealIndependently));
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using (var seed = factory.CreateDbContext())
        {
            AddPendingEvents(seed, AuditChannel.AuthnInteractive, tenantA, count: 2, startAt: 0);
            AddPendingEvents(seed, AuditChannel.AuthnInteractive, tenantB, count: 2, startAt: 10);
        }

        var options = new AuditSealerOptions { MaxSegmentSize = 2 };
        await CreateSut(factory, sealerOptions: options).RunPassAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        var segments = await db.AuditSegments.ToListAsync();
        Assert.Equal(2, segments.Count);
        Assert.All(segments, s => Assert.Equal(1, s.SequenceStart));
        Assert.All(segments, s => Assert.Equal(2, s.SequenceEnd));
        Assert.Equal(2, segments.Select(s => s.TenantId).Distinct().Count());
    }

    [Fact]
    public async Task RunPassAsync_SignsCheckpoint_OnceSegmentsPerCheckpointThresholdMet()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_SignsCheckpoint_OnceSegmentsPerCheckpointThresholdMet));
        await using (var seed = factory.CreateDbContext())
            AddPendingEvents(seed, AuditChannel.AdminConfig, tenantId: null, count: 1);

        var signer = new FakeAuditCheckpointSigner();
        var options = new AuditSealerOptions { MaxSegmentSize = 1, MaxSegmentsPerCheckpoint = 1 };
        await CreateSut(factory, signer: signer, sealerOptions: options).RunPassAsync(CancellationToken.None);

        Assert.Equal(1, signer.CallCount);

        await using var db = factory.CreateDbContext();
        var checkpoint = await db.AuditCheckpoints.SingleAsync();
        var segment = await db.AuditSegments.SingleAsync();
        Assert.Equal(segment.Id, checkpoint.SegmentId);
        Assert.Equal(1, checkpoint.TreeSize);
        Assert.Equal(segment.HeaderHash, checkpoint.RootHash);
        Assert.Equal("fake-kid", checkpoint.Kid);
    }

    [Fact]
    public async Task RunPassAsync_DoesNotResignCheckpoint_WhenNoNewSegmentsSinceLastOne()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_DoesNotResignCheckpoint_WhenNoNewSegmentsSinceLastOne));
        await using (var seed = factory.CreateDbContext())
            AddPendingEvents(seed, AuditChannel.AdminConfig, tenantId: null, count: 1);

        var signer = new FakeAuditCheckpointSigner();
        var options = new AuditSealerOptions { MaxSegmentSize = 1, MaxSegmentsPerCheckpoint = 1 };
        var sut = CreateSut(factory, signer: signer, sealerOptions: options);

        await sut.RunPassAsync(CancellationToken.None);
        await sut.RunPassAsync(CancellationToken.None);

        Assert.Equal(1, signer.CallCount);
        await using var db = factory.CreateDbContext();
        Assert.Equal(1, await db.AuditCheckpoints.CountAsync());
    }

    [Fact]
    public async Task RunPassAsync_SignsCheckpoint_InvokesEveryConfiguredWitness()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_SignsCheckpoint_InvokesEveryConfiguredWitness));
        await using (var seed = factory.CreateDbContext())
            AddPendingEvents(seed, AuditChannel.AdminConfig, tenantId: null, count: 1);

        var options = new AuditSealerOptions { MaxSegmentSize = 1, MaxSegmentsPerCheckpoint = 1 };
        var witness = new FakeAuditCheckpointWitness();
        await CreateSut(factory, sealerOptions: options, witnesses: [witness]).RunPassAsync(CancellationToken.None);

        Assert.Single(witness.Witnessed);
        Assert.Contains("warden-audit/platform/adminconfig", witness.Witnessed[0]);
    }

    [Fact]
    public async Task RunPassAsync_WitnessThrows_DoesNotPreventCheckpointFromBeingRecorded()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_WitnessThrows_DoesNotPreventCheckpointFromBeingRecorded));
        await using (var seed = factory.CreateDbContext())
            AddPendingEvents(seed, AuditChannel.AdminConfig, tenantId: null, count: 1);

        var options = new AuditSealerOptions { MaxSegmentSize = 1, MaxSegmentsPerCheckpoint = 1 };
        var failingWitness = new FakeAuditCheckpointWitness { ThrowOnWitness = true };
        await CreateSut(factory, sealerOptions: options, witnesses: [failingWitness]).RunPassAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        Assert.Equal(1, await db.AuditCheckpoints.CountAsync());
    }
}
