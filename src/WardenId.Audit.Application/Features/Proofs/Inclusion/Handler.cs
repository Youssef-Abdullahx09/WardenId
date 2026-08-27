using FluentResults;
using MediatR;
using WardenId.Audit.Application.Abstractions;
using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Features.Proofs.Inclusion;

public sealed class Handler(IAuditProofRepository repository) : IRequestHandler<Query, Result<InclusionProofResult>>
{
    public async Task<Result<InclusionProofResult>> Handle(Query request, CancellationToken cancellationToken)
    {
        var eventRow = await repository.FindEventAsync(request.EventId, cancellationToken);
        if (eventRow is null) return Result.Fail(AuditErrors.NotFound);
        if (eventRow.Sequence is null)
            return Result.Fail(AuditErrors.Validation("Event has not been sealed into a segment yet."));

        var sequence = eventRow.Sequence.Value;
        var segment = await repository.FindCoveringSegmentAsync(
            eventRow.Channel, eventRow.TenantId, sequence, cancellationToken);
        if (segment is null)
            return Result.Fail(AuditErrors.Validation("No sealed segment covers this event's sequence yet."));

        var leafHashes = await repository.GetSegmentLeafHashesAsync(
            eventRow.Channel, eventRow.TenantId, segment.SequenceStart, segment.SequenceEnd, cancellationToken);

        var expectedCount = segment.SequenceEnd - segment.SequenceStart + 1;
        if (leafHashes.Count != expectedCount)
            return Result.Fail(AuditErrors.Validation(
                $"Segment {segment.Id} has {leafHashes.Count} readable events but expects {expectedCount} — sequence density violated."));

        var leafIndex = (int)(sequence - segment.SequenceStart);
        var auditPath = MerkleInclusionProof.Compute(leafHashes, leafIndex);

        var coveringCheckpoint = await FindCoveringCheckpointAsync(eventRow.Channel, eventRow.TenantId, segment.Id, cancellationToken);

        return Result.Ok(new InclusionProofResult(
            eventRow.Id,
            sequence,
            eventRow.Payload,
            eventRow.LeafHash,
            segment.Id,
            leafIndex,
            leafHashes.Count,
            auditPath,
            coveringCheckpoint));
    }

    private async Task<CoveringCheckpoint?> FindCoveringCheckpointAsync(
        AuditChannel channel, Guid? tenantId, Guid segmentId, CancellationToken cancellationToken)
    {
        var segments = await repository.GetSegmentsAsync(channel, tenantId, cancellationToken);
        var ordinal = segments.ToList().FindIndex(s => s.Id == segmentId) + 1;
        if (ordinal <= 0) return null;

        var checkpoints = await repository.GetCheckpointsAsync(channel, tenantId, cancellationToken);
        var covering = checkpoints.FirstOrDefault(c => c.TreeSize >= ordinal);
        return covering is null
            ? null
            : new CoveringCheckpoint(covering.Id, covering.TreeSize, covering.RootHash, covering.Kid, covering.Signature, covering.SignedAt);
    }
}
