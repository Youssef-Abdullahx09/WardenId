namespace WardenId.Audit.Infrastructure.Sealing;

/// <summary>
/// One ES256 keypair used to sign checkpoints — audit.checkpoint_signing_keys. Plain
/// Infrastructure-only class, not a Domain entity: this module has no rich lifecycle for it
/// yet (rotation lands with spec §6's "rotation must itself be an audited event", deferred).
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
}
