using WardenId.Audit.Application.Features.Proofs.Inclusion;

namespace WardenId.Audit.Application.Tests.Features.Proofs.Inclusion;

public class HandlerTests
{
    private static readonly Guid? TenantId = Guid.NewGuid();
    private const AuditChannel Channel = AuditChannel.AdminConfig;

    [Fact]
    public async Task Handle_SealedEvent_ReturnsProofThatReconstructsTheSegmentRoot()
    {
        var repo = new FakeAuditProofRepository();
        var (events, segment) = ChainFixture.BuildSegment(Channel, TenantId, sequenceStart: 1, count: 5, null, DateTimeOffset.UtcNow);
        repo.Events.AddRange(events);
        repo.Segments.Add(segment);

        var target = events[2];
        var result = await new Handler(repo).Handle(new Query(target.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var proof = result.Value;
        Assert.Equal(2, proof.LeafIndex);
        Assert.Equal(5, proof.SegmentSize);
        var reconstructed = MerkleInclusionProof.ReconstructRoot(proof.LeafHash, proof.LeafIndex, proof.SegmentSize, proof.AuditPath);
        Assert.Equal(segment.MerkleRoot, reconstructed);
        Assert.Null(proof.CoveringCheckpoint);
    }

    [Fact]
    public async Task Handle_UnknownEventId_ReturnsNotFound()
    {
        var repo = new FakeAuditProofRepository();
        var result = await new Handler(repo).Handle(new Query(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(AuditErrorCodes.NotFound, result.Errors[0].Metadata[AuditErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_EventNotYetSealed_ReturnsValidationError()
    {
        var repo = new FakeAuditProofRepository();
        var eventId = Guid.NewGuid();
        repo.Events.Add(new AuditEventRow(eventId, Channel, TenantId, Sequence: null, Payload: [1], LeafHash: [2]));

        var result = await new Handler(repo).Handle(new Query(eventId), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(AuditErrorCodes.Validation, result.Errors[0].Metadata[AuditErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_NoCoveringSegmentYet_ReturnsValidationError()
    {
        var repo = new FakeAuditProofRepository();
        var eventId = Guid.NewGuid();
        repo.Events.Add(new AuditEventRow(eventId, Channel, TenantId, Sequence: 1, Payload: [1], LeafHash: ChainFixture.Leaf(1)));

        var result = await new Handler(repo).Handle(new Query(eventId), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_SequenceDensityViolated_ReturnsValidationError()
    {
        var repo = new FakeAuditProofRepository();
        var (events, segment) = ChainFixture.BuildSegment(Channel, TenantId, sequenceStart: 1, count: 5, null, DateTimeOffset.UtcNow);
        repo.Events.AddRange(events);
        repo.Events.RemoveAt(2); // simulate a deleted row — sequence gap
        repo.Segments.Add(segment);

        var result = await new Handler(repo).Handle(new Query(events[0].Id), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_ReturnsEarliestCoveringCheckpoint_ByOrdinalSegmentPosition()
    {
        var repo = new FakeAuditProofRepository();
        var (events1, segment1) = ChainFixture.BuildSegment(Channel, TenantId, 1, 3, null, DateTimeOffset.UtcNow);
        var (events2, segment2) = ChainFixture.BuildSegment(Channel, TenantId, 4, 3, segment1.HeaderHash, DateTimeOffset.UtcNow);
        repo.Events.AddRange(events1);
        repo.Events.AddRange(events2);
        repo.Segments.Add(segment1);
        repo.Segments.Add(segment2);

        var key = ChainFixture.CreateSigningKey();
        // Checkpoint covers only the first segment (TreeSize = 1).
        var checkpoint = ChainFixture.SignCheckpoint(
            key, Channel, TenantId, segment1.Id, treeSize: 1, MerkleTree.ComputeRoot([segment1.HeaderHash]), DateTimeOffset.UtcNow);
        repo.Checkpoints.Add(checkpoint);

        var resultForSegment1Event = await new Handler(repo).Handle(new Query(events1[0].Id), CancellationToken.None);
        var resultForSegment2Event = await new Handler(repo).Handle(new Query(events2[0].Id), CancellationToken.None);

        Assert.Equal(checkpoint.Id, resultForSegment1Event.Value.CoveringCheckpoint!.Id);
        Assert.Null(resultForSegment2Event.Value.CoveringCheckpoint); // not yet checkpointed
    }
}
