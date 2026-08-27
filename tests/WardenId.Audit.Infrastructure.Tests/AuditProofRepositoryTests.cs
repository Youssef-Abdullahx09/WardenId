using Microsoft.EntityFrameworkCore;

namespace WardenId.Audit.Infrastructure.Tests;

public class AuditProofRepositoryTests
{
    private sealed class InMemoryAuditDbContextFactory(string databaseName) : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(databaseName).Options);
    }

    [Fact]
    public async Task FindEventAsync_ReturnsRow_WithChannelAndSequenceMapped()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(FindEventAsync_ReturnsRow_WithChannelAndSequenceMapped));
        var eventId = Guid.NewGuid();
        await using (var db = factory.CreateDbContext())
        {
            db.AuditEvents.Add(new AuditEventRecord
            {
                Id = eventId, Channel = AuditChannel.AdminConfig.ToString(), Sequence = 5,
                TenantId = null, OccurredAt = DateTimeOffset.UtcNow, Payload = [9], LeafHash = [1, 2]
            });
            await db.SaveChangesAsync();
        }

        var row = await new AuditProofRepository(factory).FindEventAsync(eventId);

        Assert.NotNull(row);
        Assert.Equal(AuditChannel.AdminConfig, row!.Channel);
        Assert.Equal(5, row.Sequence);
    }

    [Fact]
    public async Task GetSegmentLeafHashesAsync_OrdersBySequence_AndExcludesUnsealedRows()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(GetSegmentLeafHashesAsync_OrdersBySequence_AndExcludesUnsealedRows));
        await using (var db = factory.CreateDbContext())
        {
            db.AuditEvents.AddRange(
                new AuditEventRecord { Id = Guid.NewGuid(), Channel = AuditChannel.AdminConfig.ToString(), Sequence = 2, OccurredAt = DateTimeOffset.UtcNow, LeafHash = [2] },
                new AuditEventRecord { Id = Guid.NewGuid(), Channel = AuditChannel.AdminConfig.ToString(), Sequence = 1, OccurredAt = DateTimeOffset.UtcNow, LeafHash = [1] },
                new AuditEventRecord { Id = Guid.NewGuid(), Channel = AuditChannel.AdminConfig.ToString(), Sequence = null, OccurredAt = DateTimeOffset.UtcNow, LeafHash = [9] });
            await db.SaveChangesAsync();
        }

        var hashes = await new AuditProofRepository(factory).GetSegmentLeafHashesAsync(AuditChannel.AdminConfig, null, 1, 2);

        Assert.Equal([new byte[] { 1 }, new byte[] { 2 }], hashes);
    }

    [Fact]
    public async Task GetSegmentsAsync_And_GetCheckpointsAsync_OrderCorrectly()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(GetSegmentsAsync_And_GetCheckpointsAsync_OrderCorrectly));
        var segment1 = Guid.NewGuid();
        var segment2 = Guid.NewGuid();
        await using (var db = factory.CreateDbContext())
        {
            db.AuditSegments.AddRange(
                new AuditSegmentRecord { Id = segment2, Channel = AuditChannel.AdminConfig.ToString(), SequenceStart = 4, SequenceEnd = 6, MerkleRoot = [1], HeaderHash = [2], SealedAt = DateTimeOffset.UtcNow },
                new AuditSegmentRecord { Id = segment1, Channel = AuditChannel.AdminConfig.ToString(), SequenceStart = 1, SequenceEnd = 3, MerkleRoot = [1], HeaderHash = [2], SealedAt = DateTimeOffset.UtcNow });
            db.AuditCheckpoints.AddRange(
                new AuditCheckpointRecord { Id = Guid.NewGuid(), SegmentId = segment2, Channel = AuditChannel.AdminConfig.ToString(), TreeSize = 2, RootHash = [1], Kid = "k", Signature = [1], SignedAt = DateTimeOffset.UtcNow },
                new AuditCheckpointRecord { Id = Guid.NewGuid(), SegmentId = segment1, Channel = AuditChannel.AdminConfig.ToString(), TreeSize = 1, RootHash = [1], Kid = "k", Signature = [1], SignedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var repo = new AuditProofRepository(factory);
        var segments = await repo.GetSegmentsAsync(AuditChannel.AdminConfig, null);
        var checkpoints = await repo.GetCheckpointsAsync(AuditChannel.AdminConfig, null);

        Assert.Equal([segment1, segment2], segments.Select(s => s.Id));
        Assert.Equal([1, 2], checkpoints.Select(c => c.TreeSize));
    }

    [Fact]
    public async Task GetCheckpointKeysAsync_MapsKidFromGuidId()
    {
        var factory = new InMemoryAuditDbContextFactory(nameof(GetCheckpointKeysAsync_MapsKidFromGuidId));
        var keyId = Guid.NewGuid();
        await using (var db = factory.CreateDbContext())
        {
            db.AuditCheckpointSigningKeys.Add(new WardenId.Audit.Infrastructure.Sealing.AuditCheckpointSigningKeyRecord
            {
                Id = keyId, PublicKeyJwk = "{}", EncryptedPrivateKey = [1], IsActive = true, CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var keys = await new AuditProofRepository(factory).GetCheckpointKeysAsync();

        var key = Assert.Single(keys);
        Assert.Equal(keyId.ToString("N"), key.Kid);
    }
}
