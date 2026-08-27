namespace WardenId.Audit.Domain;

/// <summary>
/// RFC 6962 §2.1.2 consistency proof — proves the tree of size m is a strict prefix of the
/// tree of size n (m &lt;= n), i.e. nothing before position m was altered, reordered, or
/// removed by whatever grew the tree from m to n. Compute and Verify mirror the same
/// recursive SUBPROOF split so they can only disagree if the underlying data is actually
/// inconsistent, never because the two sides drifted apart from each other.
/// </summary>
public static class MerkleConsistencyProof
{
    /// <summary>Proof that leaves[0:earlierSize] is a prefix of leaves (size = laterSize = leaves.Count).</summary>
    public static IReadOnlyList<byte[]> Compute(IReadOnlyList<byte[]> leaves, int earlierSize)
    {
        if (earlierSize <= 0 || earlierSize > leaves.Count)
            throw new ArgumentOutOfRangeException(nameof(earlierSize));

        return earlierSize == leaves.Count ? [] : SubProof(leaves, 0, leaves.Count, earlierSize, top: true);
    }

    /// <summary>
    /// Recomputes both the earlier and later roots the proof implies, given the earlier
    /// root as an externally-trusted seed (the checkpoint the caller already trusts). The
    /// seed is NOT always echoed through to EarlierRoot or cross-checked internally — for
    /// some (earlierSize, laterSize) pairs the earlier subtree is fully re-derived from
    /// proof-embedded hashes without ever consulting the seed. The caller MUST independently
    /// compare both EarlierRoot and LaterRoot against its own two known checkpoint root
    /// hashes; this method only proves the two are internally consistent with each other via
    /// the proof, not that either one is the value the caller actually expected.
    /// </summary>
    public static (byte[] EarlierRoot, byte[] LaterRoot) Reconstruct(
        int earlierSize, int laterSize, byte[] trustedEarlierRoot, IReadOnlyList<byte[]> proof)
    {
        if (earlierSize <= 0 || earlierSize > laterSize)
            throw new ArgumentOutOfRangeException(nameof(earlierSize));

        if (earlierSize == laterSize)
        {
            if (proof.Count != 0)
                throw new ArgumentException("A consistency proof between equal tree sizes must be empty.", nameof(proof));
            return (trustedEarlierRoot, trustedEarlierRoot);
        }

        var (earlier, later, consumed) = ReconstructSub(laterSize, earlierSize, top: true, proof, 0, trustedEarlierRoot);
        if (consumed != proof.Count)
            throw new ArgumentException("Consistency proof has more elements than this size pair requires.", nameof(proof));

        return (earlier, later);
    }

    private static List<byte[]> SubProof(IReadOnlyList<byte[]> leaves, int start, int n, int m, bool top)
    {
        if (m == n)
            return top ? [] : [MerkleTree.ComputeRoot(Slice(leaves, start, n))];

        var k = LargestPowerOfTwoLessThan(n);
        if (m <= k)
        {
            var sub = SubProof(leaves, start, k, m, top);
            sub.Add(MerkleTree.ComputeRoot(Slice(leaves, start + k, n - k)));
            return sub;
        }
        else
        {
            var sub = SubProof(leaves, start + k, n - k, m - k, top: false);
            sub.Add(MerkleTree.ComputeRoot(Slice(leaves, start, k)));
            return sub;
        }
    }

    /// <summary>Returns (earlierSubtreeRoot, laterSubtreeRoot, nextProofIndex) for this (n, m, top) frame.</summary>
    private static (byte[] Earlier, byte[] Later, int Next) ReconstructSub(
        int n, int m, bool top, IReadOnlyList<byte[]> proof, int index, byte[] trustedEarlierRoot)
    {
        if (m == n)
        {
            if (top) return (trustedEarlierRoot, trustedEarlierRoot, index);
            RequireIndex(proof, index);
            return (proof[index], proof[index], index + 1);
        }

        var k = LargestPowerOfTwoLessThan(n);
        if (m <= k)
        {
            var (subEarlier, subLater, nextIndex) = ReconstructSub(k, m, top, proof, index, trustedEarlierRoot);
            RequireIndex(proof, nextIndex);
            var rightHash = proof[nextIndex];
            // The whole old (m-sized) prefix lies inside the left child when m <= k, so the
            // right child is entirely new material — it never contributes to the earlier root.
            return (subEarlier, MerkleHash.Internal(subLater, rightHash), nextIndex + 1);
        }
        else
        {
            var (subEarlier, subLater, nextIndex) = ReconstructSub(n - k, m - k, false, proof, index, trustedEarlierRoot);
            RequireIndex(proof, nextIndex);
            var leftHash = proof[nextIndex];
            // m > k means the old prefix extends past the split, so the entire left child is
            // already part of the earlier tree too.
            return (MerkleHash.Internal(leftHash, subEarlier), MerkleHash.Internal(leftHash, subLater), nextIndex + 1);
        }
    }

    private static void RequireIndex(IReadOnlyList<byte[]> proof, int index)
    {
        if (index >= proof.Count)
            throw new ArgumentException("Consistency proof is too short for this size pair.", nameof(proof));
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
