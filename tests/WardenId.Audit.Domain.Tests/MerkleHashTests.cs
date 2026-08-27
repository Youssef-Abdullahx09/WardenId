using System.Security.Cryptography;
using System.Text;

namespace WardenId.Audit.Domain.Tests;

public class MerkleHashTests
{
    [Fact]
    public void Leaf_MatchesRfc6962Formula()
    {
        var canonicalBytes = Encoding.UTF8.GetBytes("some-canonical-payload");

        var expected = SHA256.HashData([0x00, .. canonicalBytes]);

        Assert.Equal(expected, MerkleHash.Leaf(canonicalBytes));
    }

    [Fact]
    public void Internal_MatchesRfc6962Formula()
    {
        var left = SHA256.HashData(Encoding.UTF8.GetBytes("left"));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes("right"));

        var expected = SHA256.HashData([0x01, .. left, .. right]);

        Assert.Equal(expected, MerkleHash.Internal(left, right));
    }

    [Fact]
    public void Leaf_And_Internal_OfSameBytes_NeverCollide()
    {
        // Domain separation (I1): without the prefix byte, Leaf(x) could equal Internal(a, b)
        // for some split of x into a/b — the exact second-preimage attack RFC 6962 prevents.
        var bytes = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");
        var half = bytes.Length / 2;
        var left = bytes[..half];
        var right = bytes[half..];

        var leafOfWhole = MerkleHash.Leaf(bytes);
        var internalOfHalves = MerkleHash.Internal(left, right);

        Assert.NotEqual(leafOfWhole, internalOfHalves);
    }

    [Fact]
    public void Internal_IsNotCommutative()
    {
        var left = SHA256.HashData(Encoding.UTF8.GetBytes("left"));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes("right"));

        Assert.NotEqual(MerkleHash.Internal(left, right), MerkleHash.Internal(right, left));
    }
}
