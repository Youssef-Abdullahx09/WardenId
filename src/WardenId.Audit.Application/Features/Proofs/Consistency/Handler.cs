using FluentResults;
using MediatR;
using WardenId.Audit.Application.Abstractions;
using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Features.Proofs.Consistency;

public sealed class Handler(IAuditProofRepository repository) : IRequestHandler<Query, Result<ConsistencyProofResult>>
{
    public async Task<Result<ConsistencyProofResult>> Handle(Query request, CancellationToken cancellationToken)
    {
        if (request.EarlierSize <= 0 || request.EarlierSize > request.LaterSize)
            return Result.Fail(AuditErrors.Validation("earlierSize must be positive and not greater than laterSize."));

        var checkpoints = await repository.GetCheckpointsAsync(request.Channel, request.TenantId, cancellationToken);
        var earlier = checkpoints.FirstOrDefault(c => c.TreeSize == request.EarlierSize);
        var later = checkpoints.FirstOrDefault(c => c.TreeSize == request.LaterSize);
        if (earlier is null || later is null)
            return Result.Fail(AuditErrors.Validation("No checkpoint exists with the requested tree size(s)."));

        var segments = await repository.GetSegmentsAsync(request.Channel, request.TenantId, cancellationToken);
        if (segments.Count < request.LaterSize)
            return Result.Fail(AuditErrors.Validation("Fewer sealed segments exist than the later checkpoint's tree size."));

        var headerHashes = segments.Take((int)request.LaterSize).Select(s => s.HeaderHash).ToList();
        var proof = MerkleConsistencyProof.Compute(headerHashes, (int)request.EarlierSize);

        return Result.Ok(new ConsistencyProofResult(
            new CheckpointSummary(earlier.Id, earlier.TreeSize, earlier.RootHash, earlier.Kid, earlier.Signature, earlier.SignedAt),
            new CheckpointSummary(later.Id, later.TreeSize, later.RootHash, later.Kid, later.Signature, later.SignedAt),
            proof));
    }
}
