namespace WardenId.Audit.Domain;

/// <summary>
/// RFC 6962 §2.1 Merkle Tree Hash (MTH) — folds an ordered list of already-computed leaf
/// hashes into a single root. This is NOT naive pairwise combination with last-node
/// duplication for odd counts (the older, flawed Bitcoin-style scheme) — for n &gt; 1 leaves,
/// the split point k is the largest power of two strictly less than n, giving
/// MTH(D[0:n]) = Internal(MTH(D[0:k]), MTH(D[k:n])). Reused for both a segment's own root
/// (over event leaf hashes) and a checkpoint's root (over segment header hashes) — same
/// algorithm, different leaf set.
/// </summary>
public static class MerkleTree
{
    public static byte[] ComputeRoot(IReadOnlyList<byte[]> leafHashes)
    {
        if (leafHashes.Count == 0)
            throw new ArgumentException("Cannot compute a Merkle root over zero leaves.", nameof(leafHashes));

        return Hash(leafHashes, 0, leafHashes.Count);
    }

    private static byte[] Hash(IReadOnlyList<byte[]> leaves, int start, int count)
    {
        if (count == 1) return leaves[start];

        var k = LargestPowerOfTwoLessThan(count);
        var left = Hash(leaves, start, k);
        var right = Hash(leaves, start + k, count - k);
        return MerkleHash.Internal(left, right);
    }

    private static int LargestPowerOfTwoLessThan(int n)
    {
        var k = 1;
        while (k * 2 < n) k *= 2;
        return k;
    }
}
