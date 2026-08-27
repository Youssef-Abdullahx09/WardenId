using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;

namespace WardenId.Audit.Domain;

/// <summary>
/// One sealed batch of events for a channel — see docs/audit/audit-module-spec.md §6.
/// HeaderHash is computed here, not supplied by the caller: it must always be
/// H(canonical header bytes), the same guarantee AuditEvent gives its leaf hash (I2's
/// reasoning applied one level up). Chaining (H(header_i) == header_{i+1}.PreviousHeaderHash)
/// is enforced by the sealer, which threads PreviousHeaderHash from the last sealed segment
/// for the same channel; the first segment in a channel has PreviousHeaderHash = null.
/// </summary>
public sealed record AuditSegment
{
    private static readonly JsonSerializerOptions HeaderOptions = new() { WriteIndented = false };

    public Guid Id { get; }
    public AuditChannel Channel { get; }
    public Guid? TenantId { get; }
    public long SequenceStart { get; }
    public long SequenceEnd { get; }
    public byte[] MerkleRoot { get; }
    public byte[]? PreviousHeaderHash { get; }
    public byte[] HeaderHash { get; }
    public DateTimeOffset SealedAt { get; }

    private AuditSegment(
        Guid id, AuditChannel channel, Guid? tenantId, long sequenceStart, long sequenceEnd,
        byte[] merkleRoot, byte[]? previousHeaderHash, byte[] headerHash, DateTimeOffset sealedAt)
    {
        Id = id;
        Channel = channel;
        TenantId = tenantId;
        SequenceStart = sequenceStart;
        SequenceEnd = sequenceEnd;
        MerkleRoot = merkleRoot;
        PreviousHeaderHash = previousHeaderHash;
        HeaderHash = headerHash;
        SealedAt = sealedAt;
    }

    public static Result<AuditSegment> Create(
        Guid id, AuditChannel channel, Guid? tenantId, long sequenceStart, long sequenceEnd,
        byte[] merkleRoot, byte[]? previousHeaderHash, DateTimeOffset sealedAt)
    {
        if (sequenceEnd < sequenceStart) return Result.Fail(AuditErrors.SegmentSequenceRangeInvalid());
        if (merkleRoot.Length == 0) return Result.Fail(AuditErrors.SegmentMerkleRootRequired());

        var headerHash = ComputeHeaderHash(
            id, channel, tenantId, sequenceStart, sequenceEnd, merkleRoot, previousHeaderHash, sealedAt);

        return Result.Ok(new AuditSegment(
            id, channel, tenantId, sequenceStart, sequenceEnd, merkleRoot, previousHeaderHash, headerHash, sealedAt));
    }

    private static byte[] ComputeHeaderHash(
        Guid id, AuditChannel channel, Guid? tenantId, long sequenceStart, long sequenceEnd,
        byte[] merkleRoot, byte[]? previousHeaderHash, DateTimeOffset sealedAt)
    {
        var header = new CanonicalSegmentHeader(
            id, (int)channel, tenantId, sequenceStart, sequenceEnd, merkleRoot, previousHeaderHash,
            sealedAt.ToUniversalTime().ToUnixTimeMilliseconds());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(header, HeaderOptions);
        return MerkleHash.Header(bytes);
    }
}

internal sealed record CanonicalSegmentHeader(
    [property: JsonPropertyOrder(0)] Guid Id,
    [property: JsonPropertyOrder(1)] int Channel,
    [property: JsonPropertyOrder(2)] Guid? TenantId,
    [property: JsonPropertyOrder(3)] long SequenceStart,
    [property: JsonPropertyOrder(4)] long SequenceEnd,
    [property: JsonPropertyOrder(5)] byte[] MerkleRoot,
    [property: JsonPropertyOrder(6)] byte[]? PreviousHeaderHash,
    [property: JsonPropertyOrder(7)] long SealedAtUnixMs);
