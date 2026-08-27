using System.Text;
using FluentResults;

namespace WardenId.Audit.Domain;

/// <summary>
/// A periodic signed attestation over the segment-header chain for one channel — see
/// docs/audit/audit-module-spec.md §6 "Signing". Two-step construction, since the signature
/// can't exist until after signing but the note text it signs needs the checkpoint's own
/// fields: call BuildUnsignedBody to get the bytes an Infrastructure-layer signer must sign,
/// then Create with the resulting signature.
/// </summary>
public sealed record AuditCheckpoint
{
    public Guid Id { get; }
    public AuditChannel Channel { get; }
    public Guid? TenantId { get; }
    public long TreeSize { get; }
    public byte[] RootHash { get; }
    public string Kid { get; }
    public byte[] Signature { get; }
    public DateTimeOffset SignedAt { get; }

    private AuditCheckpoint(
        Guid id, AuditChannel channel, Guid? tenantId, long treeSize, byte[] rootHash,
        string kid, byte[] signature, DateTimeOffset signedAt)
    {
        Id = id;
        Channel = channel;
        TenantId = tenantId;
        TreeSize = treeSize;
        RootHash = rootHash;
        Kid = kid;
        Signature = signature;
        SignedAt = signedAt;
    }

    public static Result<AuditCheckpoint> Create(
        Guid id, AuditChannel channel, Guid? tenantId, long treeSize, byte[] rootHash,
        string kid, byte[] signature, DateTimeOffset signedAt)
    {
        if (treeSize <= 0) return Result.Fail(AuditErrors.CheckpointTreeSizeMustBePositive());
        if (rootHash.Length == 0) return Result.Fail(AuditErrors.CheckpointRootHashRequired());
        if (string.IsNullOrWhiteSpace(kid)) return Result.Fail(AuditErrors.CheckpointKidRequired());
        if (signature.Length == 0) return Result.Fail(AuditErrors.CheckpointSignatureRequired());

        return Result.Ok(new AuditCheckpoint(id, channel, tenantId, treeSize, rootHash, kid, signature, signedAt));
    }

    /// <summary>The bytes an Infrastructure-layer signer must sign to produce a checkpoint's Signature.</summary>
    public static byte[] BuildUnsignedBody(AuditChannel channel, string scopeLabel, long treeSize, byte[] rootHash)
        => Encoding.UTF8.GetBytes(NoteHeader(channel, scopeLabel, treeSize, rootHash));

    /// <summary>The full signed-note text — spec §6.</summary>
    public string ToSignedNoteText(string scopeLabel)
        => NoteHeader(Channel, scopeLabel, TreeSize, RootHash) + $"\n— {Kid} {Convert.ToBase64String(Signature)}\n";

    private static string NoteHeader(AuditChannel channel, string scopeLabel, long treeSize, byte[] rootHash)
        => $"warden-audit/{scopeLabel}/{channel.ToString().ToLowerInvariant()}\n{treeSize}\n{Convert.ToBase64String(rootHash)}\n";
}
