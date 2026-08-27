using FluentResults;
using MediatR;

namespace WardenId.Audit.Application.Features.Proofs.Inclusion;

public sealed record Query(Guid EventId) : IRequest<Result<InclusionProofResult>>;

public sealed record CoveringCheckpoint(Guid Id, long TreeSize, byte[] RootHash, string Kid, byte[] Signature, DateTimeOffset SignedAt);

public sealed record InclusionProofResult(
    Guid EventId,
    long Sequence,
    byte[]? Payload,
    byte[] LeafHash,
    Guid SegmentId,
    int LeafIndex,
    int SegmentSize,
    IReadOnlyList<byte[]> AuditPath,
    CoveringCheckpoint? CoveringCheckpoint);
