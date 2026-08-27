using System.Security.Cryptography;

namespace WardenId.Audit.Domain.Tests;

public class MerkleConsistencyProofTests
{
    private static byte[][] Leaves(int count) =>
        Enumerable.Range(0, count).Select(i => SHA256.HashData([(byte)i, (byte)(i >> 8)])).ToArray();

    public static IEnumerable<object[]> SizePairs()
    {
        int[] sizes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 31, 32, 33, 100];
        foreach (var n in sizes)
        foreach (var m in Enumerable.Range(1, n))
            yield return [m, n];
    }

    [Theory]
    [MemberData(nameof(SizePairs))]
    public void ComputeThenReconstruct_MatchesDirectRoots_ForEverySizePair(int earlierSize, int laterSize)
    {
        var leaves = Leaves(laterSize);
        var expectedEarlierRoot = MerkleTree.ComputeRoot(leaves.Take(earlierSize).ToArray());
        var expectedLaterRoot = MerkleTree.ComputeRoot(leaves);

        var proof = MerkleConsistencyProof.Compute(leaves, earlierSize);
        var (earlier, later) = MerkleConsistencyProof.Reconstruct(earlierSize, laterSize, expectedEarlierRoot, proof);

        Assert.Equal(expectedEarlierRoot, earlier);
        Assert.Equal(expectedLaterRoot, later);
    }

    [Fact]
    public void EqualSizes_ProofIsEmpty_AndBothRootsEqualTheTrustedRoot()
    {
        var leaves = Leaves(10);
        var root = MerkleTree.ComputeRoot(leaves);

        var proof = MerkleConsistencyProof.Compute(leaves, 10);
        Assert.Empty(proof);

        var (earlier, later) = MerkleConsistencyProof.Reconstruct(10, 10, root, proof);
        Assert.Equal(root, earlier);
        Assert.Equal(root, later);
    }

    [Theory]
    [MemberData(nameof(SizePairs))]
    public void FullVerifierContract_DetectsTamperedProof_AcrossEverySizePair(int earlierSize, int laterSize)
    {
        // The real security check a caller must perform: recompute both roots (seeding with
        // its OWN genuinely-known earlier root — never an adversary-supplied value, that's
        // not how this API is used), then compare EACH against its own independently-known
        // checkpoint root hashes. Proved once per size pair, not just one hand-picked case.
        if (earlierSize == laterSize) return; // no proof elements exist to tamper with

        var leaves = Leaves(laterSize);
        var trueEarlierRoot = MerkleTree.ComputeRoot(leaves.Take(earlierSize).ToArray());
        var trueLaterRoot = MerkleTree.ComputeRoot(leaves);
        var tamperedProof = MerkleConsistencyProof.Compute(leaves, earlierSize).ToList();
        tamperedProof[^1] = SHA256.HashData([0xAB]);

        var (reconstructedEarlier, reconstructedLater) =
            MerkleConsistencyProof.Reconstruct(earlierSize, laterSize, trueEarlierRoot, tamperedProof);

        var verified = reconstructedEarlier.SequenceEqual(trueEarlierRoot) && reconstructedLater.SequenceEqual(trueLaterRoot);
        Assert.False(verified);
    }

    [Fact]
    public void Reconstruct_TamperedProofElement_ProducesMismatchingLaterRoot()
    {
        var leaves = Leaves(20);
        var earlierRoot = MerkleTree.ComputeRoot(leaves.Take(7).ToArray());
        var proof = MerkleConsistencyProof.Compute(leaves, 7).ToList();
        proof[^1] = SHA256.HashData([0xCD]);

        var (_, later) = MerkleConsistencyProof.Reconstruct(7, 20, earlierRoot, proof);

        Assert.NotEqual(MerkleTree.ComputeRoot(leaves), later);
    }

    [Fact]
    public void Compute_EarlierSizeOutOfRange_Throws()
    {
        var leaves = Leaves(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => MerkleConsistencyProof.Compute(leaves, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MerkleConsistencyProof.Compute(leaves, 11));
    }
}
