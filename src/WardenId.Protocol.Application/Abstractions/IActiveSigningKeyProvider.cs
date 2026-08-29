using FluentResults;

namespace WardenId.Protocol.Application.Abstractions;

public interface IActiveSigningKeyProvider
{
    Task<Result<ActiveSigningKey>> GetActiveSigningKeyAsync(Guid tenantId, CancellationToken cancellationToken);
}

public sealed record ActiveSigningKey(Guid KeyId, string Algorithm, byte[] EncryptedPrivateKey);
