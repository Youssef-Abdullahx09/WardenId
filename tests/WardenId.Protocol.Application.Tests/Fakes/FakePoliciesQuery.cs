using WardenId.Protocol.Query.Contracts;
using GetTokens = WardenId.Protocol.Query.Contracts.Features.Policies.GetTokens;

namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakePoliciesQuery : IPoliciesQuery
{
    public Result<GetTokens.Item?> GetTokensResponse { get; set; } = Result.Ok<GetTokens.Item?>(null);

    public List<GetTokens.Query> GetTokensCalls { get; } = [];

    public Task<Result<GetTokens.Item?>> GetTokens(GetTokens.Query query, CancellationToken cancellationToken)
    {
        GetTokensCalls.Add(query);
        return Task.FromResult(GetTokensResponse);
    }
}
