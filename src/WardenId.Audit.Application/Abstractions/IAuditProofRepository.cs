using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Abstractions;

public sealed record AuditEventRow(
    Guid Id, AuditChannel Channel, Guid? TenantId, long? Sequence, byte[]? Payload, byte[] LeafHash);

public sealed record SegmentRow(
    Guid Id, long SequenceStart, long SequenceEnd,
    byte[] MerkleRoot, byte[]? PreviousHeaderHash, byte[] HeaderHash, DateTimeOffset SealedAt);

public sealed record CheckpointRow(
    Guid Id, Guid SegmentId, long TreeSize, byte[] RootHash, string Kid, byte[] Signature, DateTimeOffset SignedAt);

public sealed record CheckpointKeyRow(string Kid, string PublicKeyJwk, bool IsActive, DateTimeOffset CreatedAt);

/// <summary>
/// Read-only access for proof generation and full verification (spec §7) — deliberately its
/// own repository rather than a scoped IAuditDbContext passthrough, so this module keeps the
/// same IDbContextFactory-based, non-request-scoped convention established in phase 2/3
/// (AuditRecorder, AuditSealerService) instead of introducing a second DbContext lifetime
/// just for the query side. Returns plain DTOs, not EF-mapped record types, so Application
/// never needs a project reference to Audit.Infrastructure.
/// </summary>
public interface IAuditProofRepository
{
    Task<AuditEventRow?> FindEventAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<SegmentRow?> FindCoveringSegmentAsync(
        AuditChannel channel, Guid? tenantId, long sequence, CancellationToken cancellationToken = default);

    /// <summary>Ordered by Sequence ascending. Fewer than (sequenceEnd - sequenceStart + 1) rows means a gap (I3/I6 density check).</summary>
    Task<IReadOnlyList<byte[]>> GetSegmentLeafHashesAsync(
        AuditChannel channel, Guid? tenantId, long sequenceStart, long sequenceEnd, CancellationToken cancellationToken = default);

    /// <summary>Ordered by SequenceStart ascending.</summary>
    Task<IReadOnlyList<SegmentRow>> GetSegmentsAsync(
        AuditChannel channel, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Ordered by TreeSize ascending.</summary>
    Task<IReadOnlyList<CheckpointRow>> GetCheckpointsAsync(
        AuditChannel channel, Guid? tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CheckpointKeyRow>> GetCheckpointKeysAsync(CancellationToken cancellationToken = default);
}
