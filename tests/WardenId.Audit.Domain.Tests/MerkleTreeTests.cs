using System.Text;

namespace WardenId.Audit.Domain.Tests;

public class MerkleTreeTests
{
    private static byte[] Leaf(string s) => MerkleHash.Leaf(Encoding.UTF8.GetBytes(s));

    [Fact]
    public void ComputeRoot_ThrowsOnEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => MerkleTree.ComputeRoot([]));
    }

    [Fact]
    public void ComputeRoot_SingleLeaf_IsTheLeafItself()
    {
        var leaf = Leaf("a");

        Assert.Equal(leaf, MerkleTree.ComputeRoot([leaf]));
    }

    [Fact]
    public void ComputeRoot_TwoLeaves_IsInternalOfBoth()
    {
        var a = Leaf("a");
        var b = Leaf("b");

        var expected = MerkleHash.Internal(a, b);

        Assert.Equal(expected, MerkleTree.ComputeRoot([a, b]));
    }

    [Fact]
    public void ComputeRoot_ThreeLeaves_SplitsAtLargestPowerOfTwoLessThanCount_NotNaiveDuplication()
    {
        // RFC 6962: for n=3, k=2 -> Internal(Internal(a,b), c). NOT Internal(Internal(a,b), Internal(c,c)),
        // which is the flawed Bitcoin-style "duplicate the odd node out" scheme.
        var a = Leaf("a");
        var b = Leaf("b");
        var c = Leaf("c");

        var expected = MerkleHash.Internal(MerkleHash.Internal(a, b), c);
        var wrongNaiveDuplication = MerkleHash.Internal(MerkleHash.Internal(a, b), MerkleHash.Internal(c, c));

        var actual = MerkleTree.ComputeRoot([a, b, c]);

        Assert.Equal(expected, actual);
        Assert.NotEqual(wrongNaiveDuplication, actual);
    }

    [Fact]
    public void ComputeRoot_FourLeaves_IsBalancedPairing()
    {
        var a = Leaf("a");
        var b = Leaf("b");
        var c = Leaf("c");
        var d = Leaf("d");

        var expected = MerkleHash.Internal(MerkleHash.Internal(a, b), MerkleHash.Internal(c, d));

        Assert.Equal(expected, MerkleTree.ComputeRoot([a, b, c, d]));
    }

    [Fact]
    public void ComputeRoot_FiveLeaves_SplitsAtFour()
    {
        var leaves = new[] { "a", "b", "c", "d", "e" }.Select(Leaf).ToArray();

        var left = MerkleHash.Internal(
            MerkleHash.Internal(leaves[0], leaves[1]), MerkleHash.Internal(leaves[2], leaves[3]));
        var expected = MerkleHash.Internal(left, leaves[4]);

        Assert.Equal(expected, MerkleTree.ComputeRoot(leaves));
    }

    [Fact]
    public void ComputeRoot_SameLeaves_AreDeterministic()
    {
        var leaves = new[] { "a", "b", "c", "d", "e", "f", "g" }.Select(Leaf).ToArray();

        var first = MerkleTree.ComputeRoot(leaves);
        var second = MerkleTree.ComputeRoot(leaves);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeRoot_IsSensitiveToLeafOrder()
    {
        var leaves = new[] { "a", "b", "c" }.Select(Leaf).ToArray();
        var reordered = new[] { leaves[2], leaves[1], leaves[0] };

        Assert.NotEqual(MerkleTree.ComputeRoot(leaves), MerkleTree.ComputeRoot(reordered));
    }
}
