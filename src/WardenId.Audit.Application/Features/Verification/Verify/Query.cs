using FluentResults;
using MediatR;
using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Features.Verification.Verify;

public sealed record Query(AuditChannel Channel, Guid? TenantId) : IRequest<Result<VerificationReport>>;

public sealed record SegmentVerification(
    Guid SegmentId,
    long SequenceStart,
    long SequenceEnd,
    bool SequenceDense,
    bool Contiguous,
    bool MerkleRootMatches,
    bool HeaderChainMatches)
{
    public bool Passed => SequenceDense && Contiguous && MerkleRootMatches && HeaderChainMatches;
}

public sealed record CheckpointVerification(
    Guid CheckpointId,
    long TreeSize,
    bool RootHashMatches,
    bool SignatureValid,
    bool SigningKeyKnown)
{
    public bool Passed => RootHashMatches && SignatureValid && SigningKeyKnown;
}

/// <summary>
/// Key-rotation continuity (spec §6 — verifying a checkpoint under a new key traces back to
/// the outgoing key's attestation) is NOT checked here: no rotation mechanism exists yet
/// (Es256CheckpointSigner never rotates — see docs/audit/PLAN.md phase 3). Every checkpoint
/// is checked against ITS OWN kid's known public key, which is everything there currently is
/// to verify.
/// </summary>
public sealed record VerificationReport(
    AuditChannel Channel,
    Guid? TenantId,
    IReadOnlyList<SegmentVerification> Segments,
    IReadOnlyList<CheckpointVerification> Checkpoints)
{
    public bool Verified => Segments.All(s => s.Passed) && Checkpoints.All(c => c.Passed);
}
