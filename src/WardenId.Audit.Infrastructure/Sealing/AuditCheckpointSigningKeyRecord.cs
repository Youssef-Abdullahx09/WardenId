namespace WardenId.Audit.Infrastructure.Sealing;

/// <summary>
/// One ES256 keypair used to sign checkpoints — audit.checkpoint_signing_keys. Plain
/// Infrastructure-only class, not a Domain entity. Lifecycle: created active (by the signer on first
/// use, or by Es256CheckpointKeyRotator), then deactivated by a rotation — never deleted, so
/// checkpoints it signed stay verifiable. A rotated-in key carries the outgoing key's signature over
/// its CheckpointKeyRotation statement.
/// EncryptedPrivateKey is protected via the app's single shared DataProtection key ring
/// (see docs/audit/OPEN-DECISIONS.md's sibling discussion — sharing Tenancy's keyring, not a
/// dedicated one, per explicit decision) under this module's own purpose string, so the
/// derived key material is still cryptographically distinct from Tenancy's token-signing key
/// even though both ultimately live behind the same ASP.NET Core Data Protection system.
/// </summary>
public sealed class AuditCheckpointSigningKeyRecord
{
    public Guid Id { get; set; }
    public string PublicKeyJwk { get; set; } = null!;
    public byte[] EncryptedPrivateKey { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When a rotation replaced this key; null while active.</summary>
    public DateTimeOffset? DeactivatedAt { get; set; }

    /// <summary>The key this one replaced; null for the first key.</summary>
    public Guid? PreviousKeyId { get; set; }

    /// <summary>The previous key's ES256 (P1363) signature over this key's CheckpointKeyRotation statement.</summary>
    public byte[]? RotationAttestation { get; set; }
}
