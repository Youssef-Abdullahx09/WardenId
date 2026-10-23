using WardenId.Audit.Application.Features.Verification.Verify;

namespace WardenId.Audit.Application.Tests.Features.Verification.Verify;

// Aliased inside the namespace: WardenId.Audit.Query (the Dapper read side) would otherwise shadow the request type.
using Query = WardenId.Audit.Application.Features.Verification.Verify.Query;

public class HandlerTests
{
    private static readonly Guid? TenantId = Guid.NewGuid();
    private const AuditChannel Channel = AuditChannel.AdminConfig;

    private static FakeAuditProofRepository BuildValidTwoSegmentChain(out ChainFixture.SigningKey key)
    {
        var repo = new FakeAuditProofRepository();
        var (e1, s1) = ChainFixture.BuildSegment(Channel, TenantId, 1, 3, null, DateTimeOffset.UtcNow);
        var (e2, s2) = ChainFixture.BuildSegment(Channel, TenantId, 4, 3, s1.HeaderHash, DateTimeOffset.UtcNow);
        repo.Events.AddRange(e1); repo.Events.AddRange(e2);
        repo.Segments.AddRange([s1, s2]);

        key = ChainFixture.CreateSigningKey();
        repo.Keys.Add(key.Row);
        var root = MerkleTree.ComputeRoot([s1.HeaderHash, s2.HeaderHash]);
        repo.Checkpoints.Add(ChainFixture.SignCheckpoint(key, Channel, TenantId, s2.Id, 2, root, DateTimeOffset.UtcNow));
        return repo;
    }

    [Fact]
    public async Task Handle_ValidChain_ReportsFullyVerified()
    {
        var repo = BuildValidTwoSegmentChain(out _);

        var result = await new Handler(repo).Handle(new Query(Channel, TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var report = result.Value;
        Assert.True(report.Verified);
        Assert.All(report.Segments, s => Assert.True(s.Passed));
        Assert.All(report.Checkpoints, c => Assert.True(c.Passed));
    }

    [Fact]
    public async Task Handle_EmptyScope_ReportsTriviallyVerified()
    {
        var result = await new Handler(new FakeAuditProofRepository()).Handle(new Query(Channel, TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Verified);
    }

    [Fact]
    public async Task Handle_DeletedEventRow_FailsDensityAndRootForThatSegment_ButNotTheOther()
    {
        var repo = BuildValidTwoSegmentChain(out _);
        var toRemove = repo.Events.First(e => e.Sequence == 2);
        repo.Events.Remove(toRemove);

        var report = (await new Handler(repo).Handle(new Query(Channel, TenantId), CancellationToken.None)).Value;

        Assert.False(report.Verified);
        var firstSegment = report.Segments[0];
        var secondSegment = report.Segments[1];
        Assert.False(firstSegment.SequenceDense);
        Assert.False(firstSegment.MerkleRootMatches);
        Assert.True(secondSegment.Passed);
    }

    [Fact]
    public async Task Handle_TamperedPreviousHeaderHash_FailsChainLinkageOnThatSegmentOnly()
    {
        var repo = BuildValidTwoSegmentChain(out _);
        var second = repo.Segments[1];
        var tampered = second with { PreviousHeaderHash = [0xFF, 0xFF] };
        repo.Segments[1] = tampered;

        var report = (await new Handler(repo).Handle(new Query(Channel, TenantId), CancellationToken.None)).Value;

        Assert.False(report.Verified);
        Assert.True(report.Segments[0].Passed);
        Assert.False(report.Segments[1].HeaderChainMatches);
    }

    [Fact]
    public async Task Handle_NonContiguousSegment_FailsContiguity()
    {
        var repo = BuildValidTwoSegmentChain(out _);
        var second = repo.Segments[1];
        repo.Segments[1] = second with { SequenceStart = second.SequenceStart + 1 };

        var report = (await new Handler(repo).Handle(new Query(Channel, TenantId), CancellationToken.None)).Value;

        Assert.False(report.Verified);
        Assert.False(report.Segments[1].Contiguous);
    }

    [Fact]
    public async Task Handle_TamperedCheckpointRootHash_FailsThatCheckpointOnly()
    {
        var repo = BuildValidTwoSegmentChain(out _);
        var checkpoint = repo.Checkpoints[0];
        repo.Checkpoints[0] = checkpoint with { RootHash = [0xAB, 0xCD] };

        var report = (await new Handler(repo).Handle(new Query(Channel, TenantId), CancellationToken.None)).Value;

        Assert.False(report.Verified);
        Assert.All(report.Segments, s => Assert.True(s.Passed));
        Assert.False(report.Checkpoints[0].RootHashMatches);
    }

    [Fact]
    public async Task Handle_TamperedSignature_FailsSignatureValidOnly()
    {
        var repo = BuildValidTwoSegmentChain(out _);
        var checkpoint = repo.Checkpoints[0];
        var tamperedSignature = (byte[])checkpoint.Signature.Clone();
        tamperedSignature[0] ^= 0xFF;
        repo.Checkpoints[0] = checkpoint with { Signature = tamperedSignature };

        var report = (await new Handler(repo).Handle(new Query(Channel, TenantId), CancellationToken.None)).Value;

        Assert.False(report.Verified);
        Assert.True(report.Checkpoints[0].RootHashMatches);
        Assert.False(report.Checkpoints[0].SignatureValid);
        Assert.True(report.Checkpoints[0].SigningKeyKnown);
    }

    [Fact]
    public async Task Handle_UnknownSigningKey_FailsSigningKeyKnownAndSignatureValid()
    {
        var repo = BuildValidTwoSegmentChain(out _);
        repo.Keys.Clear();

        var report = (await new Handler(repo).Handle(new Query(Channel, TenantId), CancellationToken.None)).Value;

        Assert.False(report.Verified);
        Assert.False(report.Checkpoints[0].SigningKeyKnown);
        Assert.False(report.Checkpoints[0].SignatureValid);
    }

    /// <summary>Three segments; checkpoint 1 (tree size 1) signed by the first key, checkpoint 2 (tree size 3) by <paramref name="second"/>.</summary>
    private static FakeAuditProofRepository BuildChainAcrossRotation(
        ChainFixture.SigningKey first, Func<ChainFixture.SigningKey, ChainFixture.SigningKey> rotate)
    {
        var repo = new FakeAuditProofRepository();
        var (e1, s1) = ChainFixture.BuildSegment(Channel, TenantId, 1, 2, null, DateTimeOffset.UtcNow);
        var (e2, s2) = ChainFixture.BuildSegment(Channel, TenantId, 3, 2, s1.HeaderHash, DateTimeOffset.UtcNow);
        var (e3, s3) = ChainFixture.BuildSegment(Channel, TenantId, 5, 2, s2.HeaderHash, DateTimeOffset.UtcNow);
        repo.Events.AddRange([.. e1, .. e2, .. e3]);
        repo.Segments.AddRange([s1, s2, s3]);

        var second = rotate(first);
        repo.Keys.Add(first.Row with { IsActive = false });
        repo.Keys.Add(second.Row);
        repo.Checkpoints.Add(ChainFixture.SignCheckpoint(
            first, Channel, TenantId, s1.Id, 1, MerkleTree.ComputeRoot([s1.HeaderHash]), DateTimeOffset.UtcNow));
        repo.Checkpoints.Add(ChainFixture.SignCheckpoint(
            second, Channel, TenantId, s3.Id, 3, MerkleTree.ComputeRoot([s1.HeaderHash, s2.HeaderHash, s3.HeaderHash]), DateTimeOffset.UtcNow));
        return repo;
    }

    [Fact]
    public async Task Handle_CheckpointsSignedBeforeAndAfterARotation_AllVerify()
    {
        var first = ChainFixture.CreateSigningKey(DateTimeOffset.UtcNow.AddDays(-1));
        var repo = BuildChainAcrossRotation(first, previous => ChainFixture.RotateSigningKey(previous, DateTimeOffset.UtcNow));

        var report = (await new Handler(repo).Handle(new Query(Channel, TenantId), CancellationToken.None)).Value;

        Assert.True(report.Verified);
        Assert.Equal(2, report.Checkpoints.Select(c => c.Kid).Distinct().Count());
        Assert.All(report.Checkpoints, c =>
        {
            Assert.True(c.SigningKeyKnown);
            Assert.True(c.SigningKeyContinuous);
        });
    }

    [Fact]
    public async Task Handle_KeyWhoseRotationWasNotSignedByTheOutgoingKey_FailsContinuity()
    {
        var first = ChainFixture.CreateSigningKey(DateTimeOffset.UtcNow.AddDays(-1));
        var impostor = ChainFixture.CreateSigningKey();
        var repo = BuildChainAcrossRotation(first, previous => ChainFixture.RotateSigningKey(previous, DateTimeOffset.UtcNow, attester: impostor));

        var report = (await new Handler(repo).Handle(new Query(Channel, TenantId), CancellationToken.None)).Value;

        Assert.False(report.Verified);
        Assert.True(report.Checkpoints[0].Passed);
        Assert.True(report.Checkpoints[1].SignatureValid);
        Assert.False(report.Checkpoints[1].SigningKeyContinuous);
    }

    [Fact]
    public async Task Handle_LaterKeyWithNoRotationStatement_FailsContinuity()
    {
        var first = ChainFixture.CreateSigningKey(DateTimeOffset.UtcNow.AddDays(-1));
        var repo = BuildChainAcrossRotation(first, _ => ChainFixture.CreateSigningKey(DateTimeOffset.UtcNow));

        var report = (await new Handler(repo).Handle(new Query(Channel, TenantId), CancellationToken.None)).Value;

        Assert.False(report.Verified);
        Assert.True(report.Checkpoints[0].SigningKeyContinuous);
        Assert.False(report.Checkpoints[1].SigningKeyContinuous);
    }
}
