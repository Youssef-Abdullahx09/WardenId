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

/// <param name="Kid">The key that signed the checkpoint.</param>
/// <param name="SigningKeyContinuous">
/// The signing key traces back to the first checkpoint key through rotation statements, each signed by
/// the key it replaced (spec §6) — so a key slipped into the key table without the outgoing key's
/// signature fails here even though its own signatures check out.
/// </param>
public sealed record CheckpointVerification(
    Guid CheckpointId,
    long TreeSize,
    bool RootHashMatches,
    bool SignatureValid,
    bool SigningKeyKnown,
    bool SigningKeyContinuous,
    string Kid)
{
    public bool Passed => RootHashMatches && SignatureValid && SigningKeyKnown && SigningKeyContinuous;
}

/// <summary>
/// Every checkpoint is checked against its own kid's public key, and that key against the rotation
/// chain (<see cref="CheckpointVerification.SigningKeyContinuous"/>). The chain starts at the earliest
/// key, which is trusted as published; anchoring that first key is up to the verifier (e.g. a witness's
/// copy of the first checkpoint or of the key list).
/// </summary>
public sealed record VerificationReport(
    AuditChannel Channel,
    Guid? TenantId,
    IReadOnlyList<SegmentVerification> Segments,
    IReadOnlyList<CheckpointVerification> Checkpoints)
{
    public bool Verified => Segments.All(s => s.Passed) && Checkpoints.All(c => c.Passed);
}
