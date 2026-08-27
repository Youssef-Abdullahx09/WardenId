using System.Security.Cryptography;

namespace WardenId.Audit.Domain;

/// <summary>
/// RFC 6962 domain-separated hashing. The 0x00/0x01 prefix byte is load-bearing — omitting
/// it lets an internal node's hash be replayed as some leaf's hash (second-preimage attack).
/// See docs/audit/audit-module-spec.md I1.
/// </summary>
public static class MerkleHash
{
    private const byte LeafPrefix = 0x00;
    private const byte InternalPrefix = 0x01;
    private const byte HeaderPrefix = 0x02;

    public static byte[] Leaf(byte[] canonicalBytes)
    {
        var input = new byte[canonicalBytes.Length + 1];
        input[0] = LeafPrefix;
        canonicalBytes.CopyTo(input, 1);
        return SHA256.HashData(input);
    }

    public static byte[] Internal(byte[] left, byte[] right)
    {
        var input = new byte[left.Length + right.Length + 1];
        input[0] = InternalPrefix;
        left.CopyTo(input, 1);
        right.CopyTo(input, 1 + left.Length);
        return SHA256.HashData(input);
    }

    /// <summary>
    /// A segment's chain-header hash — a different structure from the Merkle tree above (a
    /// linear hash chain, not a tree), but given its own domain-separation prefix anyway so
    /// the two hash "namespaces" can never collide or be confused for one another.
    /// </summary>
    public static byte[] Header(byte[] canonicalHeaderBytes)
    {
        var input = new byte[canonicalHeaderBytes.Length + 1];
        input[0] = HeaderPrefix;
        canonicalHeaderBytes.CopyTo(input, 1);
        return SHA256.HashData(input);
    }
}
