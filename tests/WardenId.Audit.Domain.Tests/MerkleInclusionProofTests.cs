using System.Security.Cryptography;

namespace WardenId.Audit.Domain.Tests;

public class MerkleInclusionProofTests
{
    private static byte[][] Leaves(int count) =>
        Enumerable.Range(0, count).Select(i => SHA256.HashData([(byte)i, (byte)(i >> 8)])).ToArray();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(100)]
    [InlineData(257)]
    public void ComputeThenReconstruct_MatchesDirectRoot_ForEveryLeafIndex(int count)
    {
        var leaves = Leaves(count);
        var expectedRoot = MerkleTree.ComputeRoot(leaves);

        for (var i = 0; i < count; i++)
        {
            var path = MerkleInclusionProof.Compute(leaves, i);
            var reconstructed = MerkleInclusionProof.ReconstructRoot(leaves[i], i, count, path);
            Assert.Equal(expectedRoot, reconstructed);
        }
    }

    [Fact]
    public void ReconstructRoot_TamperedLeaf_ProducesDifferentRoot()
    {
        var leaves = Leaves(10);
        var expectedRoot = MerkleTree.ComputeRoot(leaves);
        var path = MerkleInclusionProof.Compute(leaves, 4);

        var tamperedLeaf = SHA256.HashData([0xFF]);
        var reconstructed = MerkleInclusionProof.ReconstructRoot(tamperedLeaf, 4, 10, path);

        Assert.NotEqual(expectedRoot, reconstructed);
    }

    [Fact]
    public void ReconstructRoot_TamperedPathElement_ProducesDifferentRoot()
    {
        var leaves = Leaves(10);
        var expectedRoot = MerkleTree.ComputeRoot(leaves);
        var path = MerkleInclusionProof.Compute(leaves, 4).ToList();
        path[0] = SHA256.HashData([0xFF]);

        var reconstructed = MerkleInclusionProof.ReconstructRoot(leaves[4], 4, 10, path);

        Assert.NotEqual(expectedRoot, reconstructed);
    }

    [Fact]
    public void ReconstructRoot_WrongLeafIndex_ProducesDifferentRootOrThrows()
    {
        var leaves = Leaves(10);
        var expectedRoot = MerkleTree.ComputeRoot(leaves);
        var path = MerkleInclusionProof.Compute(leaves, 4);

        // Using the proof for index 4 but claiming index 5 is either caught by a length
        // mismatch or silently produces the wrong root — either way, it must never validate.
        try
        {
            var reconstructed = MerkleInclusionProof.ReconstructRoot(leaves[4], 5, 10, path);
            Assert.NotEqual(expectedRoot, reconstructed);
        }
        catch (ArgumentException)
        {
            // acceptable — path length happened not to match the different index's depth
        }
    }

    [Fact]
    public void SingleLeafTree_ProofIsEmpty_AndReconstructsToTheLeafItself()
    {
        var leaves = Leaves(1);
        var path = MerkleInclusionProof.Compute(leaves, 0);

        Assert.Empty(path);
        Assert.Equal(MerkleTree.ComputeRoot(leaves), MerkleInclusionProof.ReconstructRoot(leaves[0], 0, 1, path));
    }

    [Fact]
    public void Compute_IndexOutOfRange_Throws()
    {
        var leaves = Leaves(5);
        Assert.Throws<ArgumentOutOfRangeException>(() => MerkleInclusionProof.Compute(leaves, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => MerkleInclusionProof.Compute(leaves, -1));
    }
}
