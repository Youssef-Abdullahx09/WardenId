using FluentResults;

namespace WardenId.Audit.Domain.Tests;

public class AuditSegmentTests
{
    private static Result<AuditSegment> MakeSegment(byte[]? previousHeaderHash = null, byte[]? merkleRoot = null) =>
        AuditSegment.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AuditChannel.AuthnInteractive,
            tenantId: null,
            sequenceStart: 1,
            sequenceEnd: 100,
            merkleRoot ?? [1, 2, 3],
            previousHeaderHash,
            DateTimeOffset.Parse("2026-08-21T00:00:00Z"));

    [Fact]
    public void Create_WithSequenceEndBeforeStart_Fails()
    {
        var result = AuditSegment.Create(
            Guid.NewGuid(), AuditChannel.AuthnInteractive, null, sequenceStart: 100, sequenceEnd: 1,
            [1], null, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_WithEmptyMerkleRoot_Fails()
    {
        var result = MakeSegment(merkleRoot: []);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SameInputs_ProduceTheSameHeaderHash()
    {
        var first = MakeSegment().Value;
        var second = MakeSegment().Value;

        Assert.Equal(first.HeaderHash, second.HeaderHash);
    }

    [Fact]
    public void Create_DifferentMerkleRoot_ProducesDifferentHeaderHash()
    {
        var a = MakeSegment(merkleRoot: [1, 2, 3]).Value;
        var b = MakeSegment(merkleRoot: [4, 5, 6]).Value;

        Assert.NotEqual(a.HeaderHash, b.HeaderHash);
    }

    [Fact]
    public void Create_DifferentPreviousHeaderHash_ProducesDifferentHeaderHash()
    {
        // The chain link is part of what gets hashed — a forged/substituted predecessor
        // changes this segment's own header hash, so tampering with history is detectable
        // even without re-walking the whole chain.
        var noPredecessor = MakeSegment(previousHeaderHash: null).Value;
        var withPredecessor = MakeSegment(previousHeaderHash: [9, 9, 9]).Value;

        Assert.NotEqual(noPredecessor.HeaderHash, withPredecessor.HeaderHash);
    }

    [Fact]
    public void Chaining_SecondSegmentsPreviousHeaderHash_MatchesFirstSegmentsHeaderHash()
    {
        var first = MakeSegment().Value;

        var second = AuditSegment.Create(
            Guid.Parse("22222222-2222-2222-2222-222222222222"), AuditChannel.AuthnInteractive, null,
            sequenceStart: 101, sequenceEnd: 200, [4, 5, 6], first.HeaderHash, DateTimeOffset.UtcNow).Value;

        Assert.Equal(first.HeaderHash, second.PreviousHeaderHash);
    }
}
