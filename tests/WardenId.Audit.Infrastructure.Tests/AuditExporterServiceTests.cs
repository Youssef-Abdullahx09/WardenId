using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WardenId.Audit.Infrastructure.Exporting;
using WardenId.Audit.Infrastructure.Tests.Fakes;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>
/// Exercises AuditExporterService's segment discovery/message-building/ExportedAt-marking
/// against InMemory + FakeAuditSegmentPublisher (no hand-rolled ICapPublisher fake is practical
/// — see IAuditSegmentPublisher's doc comment). The real CapAuditSegmentPublisher has no test
/// of its own; it needs a real CAP/Postgres transaction to verify, same posture as
/// PgSealableEventReader/PgChannelAdvisoryLock.
/// </summary>
public class AuditExporterServiceTests
{
    private sealed class InMemoryAuditDbContextFactory(string databaseName) : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName)
                // AuditExporterService opens a real EF transaction to enlist the CAP publish
                // onto; InMemory has no transactions, so it just needs telling not to throw
                // on that call — the exporter's own logic under test doesn't depend on the
                // transaction actually being atomic here.
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
    }

    private static AuditExporterService CreateSut(
        InMemoryAuditDbContextFactory factory, FakeAuditSegmentPublisher publisher, AuditExporterOptions? options = null) =>
        new(factory, publisher, Options.Create(options ?? new AuditExporterOptions()), NullLogger<AuditExporterService>.Instance);

    private static AuditEventRecord Event(int i, AuditChannel channel, Guid? tenantId, long sequence) => new()
    {
        Id = new Guid(i, 0, 0, new byte[8]),
        Channel = channel.ToString(),
        TenantId = tenantId,
        Sequence = sequence,
        OccurredAt = DateTimeOffset.UtcNow,
        Payload = [(byte)i, 1, 2, 3],
        LeafHash = [(byte)(i + 1)]
    };

    private static AuditSegmentRecord Segment(AuditChannel channel, Guid? tenantId, long start, long end, DateTimeOffset? exportedAt = null) => new()
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
    public async Task RunPassAsync_PublishesUnexportedSegment_AndMarksExportedAt()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_PublishesUnexportedSegment_AndMarksExportedAt));
        var segment = Segment(AuditChannel.AdminConfig, tenantId: null, 1, 2);
        await using (var seed = factory.CreateDbContext())
        {
            seed.AuditEvents.Add(Event(1, AuditChannel.AdminConfig, null, 1));
            seed.AuditEvents.Add(Event(2, AuditChannel.AdminConfig, null, 2));
            seed.AuditSegments.Add(segment);
            await seed.SaveChangesAsync();
        }

        var publisher = new FakeAuditSegmentPublisher();
        await CreateSut(factory, publisher).RunPassAsync(CancellationToken.None);

        Assert.Single(publisher.Published);
        var message = publisher.Published[0];
        Assert.Equal(segment.Id, message.SegmentId);
        Assert.Equal(2, message.Events.Count);
        Assert.Equal([1L, 2L], message.Events.Select(e => e.Sequence));

        await using var db = factory.CreateDbContext();
        var persisted = await db.AuditSegments.SingleAsync();
        Assert.NotNull(persisted.ExportedAt);
    }

    [Fact]
    public async Task RunPassAsync_AlreadyExportedSegment_IsSkipped()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_AlreadyExportedSegment_IsSkipped));
        await using (var seed = factory.CreateDbContext())
        {
            seed.AuditEvents.Add(Event(1, AuditChannel.AdminConfig, null, 1));
            seed.AuditSegments.Add(Segment(AuditChannel.AdminConfig, null, 1, 1, exportedAt: DateTimeOffset.UtcNow));
            await seed.SaveChangesAsync();
        }

        var publisher = new FakeAuditSegmentPublisher();
        await CreateSut(factory, publisher).RunPassAsync(CancellationToken.None);

        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task RunPassAsync_SegmentMissingSomeEvents_SkipsExport_AndLeavesExportedAtNull()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_SegmentMissingSomeEvents_SkipsExport_AndLeavesExportedAtNull));
        await using (var seed = factory.CreateDbContext())
        {
            seed.AuditEvents.Add(Event(1, AuditChannel.AdminConfig, null, 1));
            // Sequence 2 missing — simulates the exporter racing ahead of full visibility.
            seed.AuditSegments.Add(Segment(AuditChannel.AdminConfig, null, 1, 2));
            await seed.SaveChangesAsync();
        }

        var publisher = new FakeAuditSegmentPublisher();
        await CreateSut(factory, publisher).RunPassAsync(CancellationToken.None);

        Assert.Empty(publisher.Published);
        await using var db = factory.CreateDbContext();
        Assert.Null(await db.AuditSegments.Select(s => s.ExportedAt).SingleAsync());
    }

    [Fact]
    public async Task RunPassAsync_OnePublisherFailure_DoesNotBlockOtherSegments()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(RunPassAsync_OnePublisherFailure_DoesNotBlockOtherSegments));
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using (var seed = factory.CreateDbContext())
        {
            seed.AuditEvents.Add(Event(1, AuditChannel.AdminConfig, tenantA, 1));
            seed.AuditSegments.Add(Segment(AuditChannel.AdminConfig, tenantA, 1, 1));
            seed.AuditEvents.Add(Event(2, AuditChannel.AdminConfig, tenantB, 1));
            seed.AuditSegments.Add(Segment(AuditChannel.AdminConfig, tenantB, 1, 1));
            await seed.SaveChangesAsync();
        }

        var publisher = new FakeAuditSegmentPublisher { ThrowOnPublish = true };
        // Failure path never marks ExportedAt; asserting the pass completes without throwing.
        await CreateSut(factory, publisher).RunPassAsync(CancellationToken.None);

        await using var db = factory.CreateDbContext();
        Assert.All(await db.AuditSegments.Select(s => s.ExportedAt).ToListAsync(), Assert.Null);
    }
}
