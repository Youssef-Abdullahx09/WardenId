using WardenId.Tenancy.Query.Contracts;
using GetActiveForSigning = WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetActiveForSigning;
using GetPublicKeys = WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetPublicKeys;

namespace WardenId.Tenancy.Application.Tests.Fakes;

public sealed class FakeSigningKeysQuery : ISigningKeysQuery
{
    public Result<GetPublicKeys.Item?> GetPublicKeysResponse { get; set; } = Result.Ok<GetPublicKeys.Item?>(null);
    public List<GetPublicKeys.Query> GetPublicKeysCalls { get; } = [];

    public Result<GetActiveForSigning.Item?> GetActiveForSigningResponse { get; set; } =
        Result.Ok<GetActiveForSigning.Item?>(null);
    public List<GetActiveForSigning.Query> GetActiveForSigningCalls { get; } = [];

    public Task<Result<GetPublicKeys.Item?>> GetPublicKeys(GetPublicKeys.Query query, CancellationToken cancellationToken)
    {
        GetPublicKeysCalls.Add(query);
        return Task.FromResult(GetPublicKeysResponse);
    }

    public Task<Result<GetActiveForSigning.Item?>> GetActiveForSigning(
        GetActiveForSigning.Query query, CancellationToken cancellationToken)
    {
        GetActiveForSigningCalls.Add(query);
        return Task.FromResult(GetActiveForSigningResponse);
    }
}
