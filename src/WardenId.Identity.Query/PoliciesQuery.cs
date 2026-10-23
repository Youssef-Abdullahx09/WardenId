using Dapper;
using FluentResults;
using WardenId.Identity.Query.Contracts;
using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Query;

namespace WardenId.Identity.Query;

public sealed class PoliciesQuery(
    IConnectionWrapper connectionWrapper,
    IReadFileService readFileService) : IPoliciesQuery
{
    private const string GetIdentitySql = "/Features/Policies/GetIdentity/Query.sql";

    public async Task<Result<Contracts.Features.Policies.GetIdentity.Item?>> GetIdentity(
        Contracts.Features.Policies.GetIdentity.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(GetIdentitySql, cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);

        var policy = await connectionWrapper.QueryFirstOrDefaultAsync<Contracts.Features.Policies.GetIdentity.Item>(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);

        return Result.Ok(policy);
    }
}
