using Dapper;
using FluentResults;
using WardenId.Protocol.Query.Contracts;
using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Query;

namespace WardenId.Protocol.Query;

public sealed class PoliciesQuery(
    IConnectionWrapper connectionWrapper,
    IReadFileService readFileService) : IPoliciesQuery
{
    private const string GetTokensSql = "/Features/Policies/GetTokens/Query.sql";

    public async Task<Result<Contracts.Features.Policies.GetTokens.Item?>> GetTokens(
        Contracts.Features.Policies.GetTokens.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            GetTokensSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);

        var overrides = await connectionWrapper.QueryFirstOrDefaultAsync<Contracts.Features.Policies.GetTokens.Item>(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);

        return Result.Ok(overrides);
    }
}
