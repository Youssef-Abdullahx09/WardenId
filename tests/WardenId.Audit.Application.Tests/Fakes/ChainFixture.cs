using System.Security.Cryptography;
using System.Text.Json;

namespace WardenId.Audit.Application.Tests.Fakes;

/// <summary>
/// Builds a genuinely internally-consistent events -> segment -> checkpoint chain for
/// handler tests, using the real domain factories (AuditSegment.Create, AuditCheckpoint.Create,
/// MerkleTree/MerkleInclusionProof/MerkleConsistencyProof) rather than hand-rolled fixtures —
/// so a bug in this fixture and a bug in the handler under test can't accidentally cancel out.
/// </summary>
public static class ChainFixture
{
    public static byte[] Leaf(long sequence) => SHA256.HashData(BitConverter.GetBytes(sequence));

    public static (List<AuditEventRow> Events, SegmentRow Segment) BuildSegment(
        AuditChannel channel, Guid? tenantId, long sequenceStart, int count, byte[]? previousHeaderHash, DateTimeOffset sealedAt)
    {
        var events = Enumerable.Range(0, count)
            .Select(i => new AuditEventRow(
                Guid.NewGuid(), channel, tenantId, sequenceStart + i, [(byte)i], Leaf(sequenceStart + i)))
            .ToList();

        var merkleRoot = MerkleTree.ComputeRoot(events.Select(e => e.LeafHash).ToList());
        var segment = AuditSegment.Create(
            Guid.NewGuid(), channel, tenantId, sequenceStart, sequenceStart + count - 1,
            merkleRoot, previousHeaderHash, sealedAt).Value;

        return (events, new SegmentRow(
            segment.Id, segment.SequenceStart, segment.SequenceEnd,
            segment.MerkleRoot, segment.PreviousHeaderHash, segment.HeaderHash, segment.SealedAt));
    }

    public sealed record SigningKey(string Kid, ECDsa Ecdsa, CheckpointKeyRow Row);

    public static SigningKey CreateSigningKey()
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var kid = Guid.NewGuid().ToString("N");
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        var jwk = JsonSerializer.Serialize(new
        {
            kty = "EC",
            crv = "P-256",
            x = Base64UrlEncode(parameters.Q.X!),
            y = Base64UrlEncode(parameters.Q.Y!),
            alg = "ES256",
            use = "sig",
            kid
        });

        return new SigningKey(kid, ecdsa, new CheckpointKeyRow(kid, jwk, IsActive: true, DateTimeOffset.UtcNow));
    }

    public static CheckpointRow SignCheckpoint(
        SigningKey key, AuditChannel channel, Guid? tenantId, Guid segmentId, long treeSize, byte[] rootHash, DateTimeOffset signedAt)
    {
        var scopeLabel = tenantId?.ToString() ?? "platform";
        var unsignedBody = AuditCheckpoint.BuildUnsignedBody(channel, scopeLabel, treeSize, rootHash);
        var signature = key.Ecdsa.SignData(unsignedBody, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        var checkpoint = AuditCheckpoint.Create(Guid.NewGuid(), channel, tenantId, treeSize, rootHash, key.Kid, signature, signedAt).Value;
        return new CheckpointRow(checkpoint.Id, segmentId, checkpoint.TreeSize, checkpoint.RootHash, checkpoint.Kid, checkpoint.Signature, checkpoint.SignedAt);
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
