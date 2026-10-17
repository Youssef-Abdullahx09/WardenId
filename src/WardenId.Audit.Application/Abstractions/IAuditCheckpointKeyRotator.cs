using FluentResults;
using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Abstractions;

/// <param name="Kid">The new active key.</param>
/// <param name="PreviousKid">The key it replaced; null when there was none yet.</param>
public sealed record CheckpointKeyRotationResult(string Kid, string? PreviousKid);

/// <summary>Rotates the checkpoint signing key (spec §6). Implemented next to the signer, which owns the key material.</summary>
public interface IAuditCheckpointKeyRotator
{
    /// <summary>
    /// Generates a new key and makes it the only active one. The old key stays, inactive, so checkpoints it
    /// signed still verify, and signs a <see cref="CheckpointKeyRotation"/> statement for the new key. The
    /// event <paramref name="buildAuditEvent"/> returns for (previous kid, new kid) is written in the same save,
    /// so a rotation that can't be recorded doesn't happen (I10). Fails with a conflict when a concurrent
    /// rotation won.
    /// </summary>
    Task<Result<CheckpointKeyRotationResult>> RotateAsync(
        Func<string?, string, AuditEvent> buildAuditEvent, CancellationToken cancellationToken = default);
}
