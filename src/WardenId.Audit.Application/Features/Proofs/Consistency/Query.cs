using FluentResults;
using MediatR;
using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Features.Proofs.Consistency;

public sealed record Query(
    AuditChannel Channel, Guid? TenantId, long EarlierSize, long LaterSize) : IRequest<Result<ConsistencyProofResult>>;

public sealed record CheckpointSummary(Guid Id, long TreeSize, byte[] RootHash, string Kid, byte[] Signature, DateTimeOffset SignedAt);

public sealed record ConsistencyProofResult(
    CheckpointSummary Earlier, CheckpointSummary Later, IReadOnlyList<byte[]> Proof);
