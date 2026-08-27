namespace WardenId.Audit.Domain;

/// <summary>
/// RFC 6962 §2.1.1 audit path — proves one leaf belongs at a specific index in a tree of a
/// given size, without needing every other leaf. Compute and VerifyRoot share the exact same
/// recursive split (see MerkleTree's MTH) so they can only ever disagree if the stored data
/// itself is wrong, never because the two algorithms drifted apart from each other.
/// </summary>
public static class MerkleInclusionProof
{
    /// <summary>Sibling hashes for leaves[leafIndex], ordered leaf-to-root (index 0 combines first).</summary>
    public static IReadOnlyList<byte[]> Compute(IReadOnlyList<byte[]> leaves, int leafIndex)
    {
        if (leaves.Count == 0)
            throw new ArgumentException("Cannot compute an inclusion proof over zero leaves.", nameof(leaves));
        if (leafIndex < 0 || leafIndex >= leaves.Count)
            throw new ArgumentOutOfRangeException(nameof(leafIndex));

        return Path(leaves, 0, leaves.Count, leafIndex);
    }

    /// <summary>
    /// Recomputes the root a valid (leafHash, leafIndex, treeSize, path) combination implies —
    /// the caller compares this against the covering segment's/checkpoint's already-trusted
    /// root hash. Never trust a "true/false" from inside this method alone; the byte
    /// comparison against the independently-known root is the actual check.
    /// </summary>
    public static byte[] ReconstructRoot(byte[] leafHash, int leafIndex, int treeSize, IReadOnlyList<byte[]> path)
    {
        if (treeSize <= 0) throw new ArgumentOutOfRangeException(nameof(treeSize));
        if (leafIndex < 0 || leafIndex >= treeSize) throw new ArgumentOutOfRangeException(nameof(leafIndex));

        var (root, consumed) = Reconstruct(leafHash, leafIndex, treeSize, path, 0);
        if (consumed != path.Count)
            throw new ArgumentException("Audit path has more elements than this leaf index/tree size requires.", nameof(path));

        return root;
    }

    private static List<byte[]> Path(IReadOnlyList<byte[]> leaves, int start, int count, int m)
    {
        if (count == 1) return [];

        var k = LargestPowerOfTwoLessThan(count);
        if (m < k)
        {
            var subPath = Path(leaves, start, k, m);
            subPath.Add(MerkleTree.ComputeRoot(Slice(leaves, start + k, count - k)));
            return subPath;
        }
        else
        {
            var subPath = Path(leaves, start + k, count - k, m - k);
            subPath.Add(MerkleTree.ComputeRoot(Slice(leaves, start, k)));
            return subPath;
        }
    }

    private static (byte[] Root, int Consumed) Reconstruct(
        byte[] leafHash, int m, int n, IReadOnlyList<byte[]> path, int pathIndex)
    {
        if (n == 1) return (leafHash, pathIndex);

        var k = LargestPowerOfTwoLessThan(n);
        if (m < k)
        {
            var (left, nextIndex) = Reconstruct(leafHash, m, k, path, pathIndex);
            RequireIndex(path, nextIndex);
            return (MerkleHash.Internal(left, path[nextIndex]), nextIndex + 1);
        }
        else
        {
            var (right, nextIndex) = Reconstruct(leafHash, m - k, n - k, path, pathIndex);
            RequireIndex(path, nextIndex);
            return (MerkleHash.Internal(path[nextIndex], right), nextIndex + 1);
        }
    }

    private static void RequireIndex(IReadOnlyList<byte[]> path, int index)
    {
        if (index >= path.Count)
            throw new ArgumentException("Audit path is too short for this leaf index/tree size.", nameof(path));
    }

    private static IReadOnlyList<byte[]> Slice(IReadOnlyList<byte[]> leaves, int start, int count)
    {
        var slice = new byte[count][];
        for (var i = 0; i < count; i++) slice[i] = leaves[start + i];
        return slice;
    }

    private static int LargestPowerOfTwoLessThan(int n)
    {
        var k = 1;
        while (k * 2 < n) k *= 2;
        return k;
    }
}
