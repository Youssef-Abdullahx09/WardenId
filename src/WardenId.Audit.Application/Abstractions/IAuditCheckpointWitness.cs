namespace WardenId.Audit.Application.Abstractions;

/// <summary>
/// External witness for a freshly signed checkpoint — spec §8's "append-only file on a separate
/// volume, and a webhook per checkpoint. Both optional." Invoked best-effort, after the
/// checkpoint already committed to audit.checkpoints — a witness failure must never roll back
/// or block the checkpoint itself, which is already the authoritative signed record. Signature
/// only mentions primitives, so this lives in Audit.Application rather than SharedKernel (same
/// reasoning as IAuditRecorder — see docs/audit/PLAN.md §5.1).
/// </summary>
public interface IAuditCheckpointWitness
{
    Task WitnessAsync(string signedNoteText, CancellationToken cancellationToken = default);
}
