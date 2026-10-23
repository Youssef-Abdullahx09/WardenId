using System.Data;
using Dapper;
using FluentResults;
using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Query;
using WardenId.Tenancy.Query.Contracts;

namespace WardenId.Tenancy.Query;

public sealed class SigningKeysQuery(
    IConnectionWrapper connectionWrapper,
    IReadFileService readFileService) : ISigningKeysQuery
{
    private const string GetPublicKeysSql = "/Features/SigningKeys/GetPublicKeys/Query.sql";
    private const string GetActiveForSigningSql = "/Features/SigningKeys/GetActiveForSigning/Query.sql";

    public async Task<Result<Contracts.Features.SigningKeys.GetPublicKeys.Item?>> GetPublicKeys(
        Contracts.Features.SigningKeys.GetPublicKeys.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            GetPublicKeysSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.Slug), query.Slug, DbType.String);

        await using var grid = await connectionWrapper.QueryMultipleAsync(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);
        var tenant = await grid.ReadSingleOrDefaultAsync<Contracts.Features.SigningKeys.GetPublicKeys.Item>();
        var publicKeys = (await grid.ReadAsync<Contracts.Features.SigningKeys.GetPublicKeys.PublicKeyItem>()).ToList();
        if (tenant is null) return Result.Ok<Contracts.Features.SigningKeys.GetPublicKeys.Item?>(null);

        tenant.PublicKeys = publicKeys;
        return Result.Ok<Contracts.Features.SigningKeys.GetPublicKeys.Item?>(tenant);
    }

    public async Task<Result<Contracts.Features.SigningKeys.GetActiveForSigning.Item?>> GetActiveForSigning(
        Contracts.Features.SigningKeys.GetActiveForSigning.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            GetActiveForSigningSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);
        queryParameters.Add(nameof(query.ActiveStatus), query.ActiveStatus, DbType.String);

        var item = await connectionWrapper.QueryFirstOrDefaultAsync<Contracts.Features.SigningKeys.GetActiveForSigning.Item>(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);

        return Result.Ok<Contracts.Features.SigningKeys.GetActiveForSigning.Item?>(item);
    }
}
