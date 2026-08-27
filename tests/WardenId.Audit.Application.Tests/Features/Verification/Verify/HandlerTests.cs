using WardenId.Audit.Application.Features.Verification.Verify;

namespace WardenId.Audit.Application.Tests.Features.Verification.Verify;

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
}
