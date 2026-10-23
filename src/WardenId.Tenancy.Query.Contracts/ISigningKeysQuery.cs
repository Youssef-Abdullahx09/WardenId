using FluentResults;

namespace WardenId.Tenancy.Query.Contracts;

/// <summary>Read-side (Dapper) queries over the Tenancy schema's signing keys. A null value means no matching tenant.</summary>
public interface ISigningKeysQuery
{
    Task<Result<Features.SigningKeys.GetPublicKeys.Item?>> GetPublicKeys(
        Features.SigningKeys.GetPublicKeys.Query query,
        CancellationToken cancellationToken);

    Task<Result<Features.SigningKeys.GetActiveForSigning.Item?>> GetActiveForSigning(
        Features.SigningKeys.GetActiveForSigning.Query query,
        CancellationToken cancellationToken);
}
