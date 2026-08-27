namespace WardenId.Audit.Infrastructure.Sealing;

/// <summary>Signs checkpoint bodies (spec §6 "Signing") with ES256, managing its own signing key.</summary>
public interface IAuditCheckpointSigner
{
    Task<(string Kid, byte[] Signature)> SignAsync(byte[] unsignedBody, CancellationToken cancellationToken = default);
}
