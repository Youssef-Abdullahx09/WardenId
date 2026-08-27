using WardenId.Audit.Application.Features.Proofs.Consistency;

namespace WardenId.Audit.Application.Tests.Features.Proofs.Consistency;

public class HandlerTests
{
    private static readonly Guid? TenantId = Guid.NewGuid();
    private const AuditChannel Channel = AuditChannel.AdminConfig;

    private static FakeAuditProofRepository BuildRepoWithThreeSegmentsAndTwoCheckpoints(out CheckpointRow earlier, out CheckpointRow later)
    {
        var repo = new FakeAuditProofRepository();
        var (e1, s1) = ChainFixture.BuildSegment(Channel, TenantId, 1, 2, null, DateTimeOffset.UtcNow);
        var (e2, s2) = ChainFixture.BuildSegment(Channel, TenantId, 3, 2, s1.HeaderHash, DateTimeOffset.UtcNow);
        var (e3, s3) = ChainFixture.BuildSegment(Channel, TenantId, 5, 2, s2.HeaderHash, DateTimeOffset.UtcNow);
        repo.Events.AddRange(e1); repo.Events.AddRange(e2); repo.Events.AddRange(e3);
        repo.Segments.AddRange([s1, s2, s3]);

        var key = ChainFixture.CreateSigningKey();
        var earlierRoot = MerkleTree.ComputeRoot([s1.HeaderHash, s2.HeaderHash]);
        var laterRoot = MerkleTree.ComputeRoot([s1.HeaderHash, s2.HeaderHash, s3.HeaderHash]);
        earlier = ChainFixture.SignCheckpoint(key, Channel, TenantId, s2.Id, 2, earlierRoot, DateTimeOffset.UtcNow);
        later = ChainFixture.SignCheckpoint(key, Channel, TenantId, s3.Id, 3, laterRoot, DateTimeOffset.UtcNow);
        repo.Checkpoints.Add(earlier);
        repo.Checkpoints.Add(later);
        return repo;
    }

    [Fact]
    public async Task Handle_ValidSizePair_ReturnsProofThatReconstructsBothRoots()
    {
        var repo = BuildRepoWithThreeSegmentsAndTwoCheckpoints(out var earlier, out var later);

        var result = await new Handler(repo).Handle(new Query(Channel, TenantId, 2, 3), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var (reconstructedEarlier, reconstructedLater) =
            MerkleConsistencyProof.Reconstruct(2, 3, earlier.RootHash, result.Value.Proof);
        Assert.Equal(earlier.RootHash, reconstructedEarlier);
        Assert.Equal(later.RootHash, reconstructedLater);
    }

    [Fact]
    public async Task Handle_EarlierSizeGreaterThanLaterSize_ReturnsValidationError()
    {
        var repo = BuildRepoWithThreeSegmentsAndTwoCheckpoints(out _, out _);
        var result = await new Handler(repo).Handle(new Query(Channel, TenantId, 3, 2), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(AuditErrorCodes.Validation, result.Errors[0].Metadata[AuditErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_NoCheckpointAtRequestedSize_ReturnsValidationError()
    {
        var repo = BuildRepoWithThreeSegmentsAndTwoCheckpoints(out _, out _);
        var result = await new Handler(repo).Handle(new Query(Channel, TenantId, 1, 3), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
