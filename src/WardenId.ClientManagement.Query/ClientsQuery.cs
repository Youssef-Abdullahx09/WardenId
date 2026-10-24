using System.Data;
using Dapper;
using FluentResults;
using WardenId.ClientManagement.Query.Contracts;
using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Query;
using WardenId.SharedKernel.Query.Contracts;
using Get = WardenId.ClientManagement.Query.Contracts.Features.Clients.Get;
using GetForAuthentication = WardenId.ClientManagement.Query.Contracts.Features.Clients.GetForAuthentication;
using GetReference = WardenId.ClientManagement.Query.Contracts.Features.Clients.GetReference;
using List = WardenId.ClientManagement.Query.Contracts.Features.Clients.List;
using ListCorsOrigins = WardenId.ClientManagement.Query.Contracts.Features.Clients.ListCorsOrigins;
using VerifySecret = WardenId.ClientManagement.Query.Contracts.Features.Clients.VerifySecret;

namespace WardenId.ClientManagement.Query;

public sealed class ClientsQuery(
    IConnectionWrapper connectionWrapper,
    IReadFileService readFileService) : IClientsQuery
{
    private const string GetSql = "/Features/Clients/Get/Query.sql";
    private const string ListSql = "/Features/Clients/List/Query.sql";
    private const string GetForAuthenticationSql = "/Features/Clients/GetForAuthentication/Query.sql";
    private const string GetReferenceSql = "/Features/Clients/GetReference/Query.sql";
    private const string ListCorsOriginsSql = "/Features/Clients/ListCorsOrigins/Query.sql";
    private const string VerifySecretSql = "/Features/Clients/VerifySecret/Query.sql";
    private const string OffsetParameter = "Offset";
    private const string DescendingParameter = "Descending";

    public async Task<Result<Get.Item?>> Get(
        Get.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            GetSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);
        queryParameters.Add(nameof(query.Id), query.Id);

        await using var grid = await connectionWrapper.QueryMultipleAsync(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);
        var client = await grid.ReadSingleOrDefaultAsync<Get.Item>();
        var secrets = (await grid.ReadAsync<Get.Secret>()).ToArray();
        var signingKeys = (await grid.ReadAsync<Get.SigningKey>()).ToArray();

        if (client is null) return Result.Ok<Get.Item?>(null);

        client.Secrets = secrets;
        client.SigningKeys = signingKeys;
        return Result.Ok<Get.Item?>(client);
    }

    public async Task<Result<PaginationListResponse<List.Item>>> List(
        List.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            ListSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);
        // Typed explicitly: an untyped NULL leaves Postgres unable to infer "@Enabled IS NULL".
        queryParameters.Add(nameof(query.Enabled), query.Enabled, DbType.Boolean);
        // Persisted by member name; Dapper would otherwise send the enum as its number.
        queryParameters.Add(nameof(query.ClientType), query.ClientType?.ToString(), DbType.String);
        queryParameters.Add(nameof(query.SortOption), query.SortOption.ToString(), DbType.String);
        queryParameters.Add(DescendingParameter, query.SortDirection == SortDirection.Desc);
        queryParameters.Add(nameof(query.PageSize), query.PageSize);
        queryParameters.Add(OffsetParameter, (query.PageNumber - 1) * query.PageSize);

        long recordsTotal = 0;

        var clients = await connectionWrapper.QueryAsync<List.Item, long, List.Item>(
            readingFileResult.Value,
            (item, count) =>
            {
                recordsTotal = count;
                return item;
            },
            splitOn: nameof(PaginationListResponse<List.Item>.RecordsTotal),
            param: queryParameters,
            cancellationToken: cancellationToken);

        return new PaginationListResponse<List.Item>(recordsTotal, clients.ToList());
    }

    public async Task<Result<GetForAuthentication.Item?>> GetForAuthentication(
        GetForAuthentication.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            GetForAuthenticationSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);
        queryParameters.Add(nameof(query.ClientId), query.ClientId, DbType.String);

        var client = await connectionWrapper.QueryFirstOrDefaultAsync<GetForAuthentication.Item>(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);

        return Result.Ok(client);
    }

    public async Task<Result<GetReference.Item?>> GetReference(
        GetReference.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            GetReferenceSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);
        queryParameters.Add(nameof(query.Id), query.Id);

        var client = await connectionWrapper.QueryFirstOrDefaultAsync<GetReference.Item>(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);

        return Result.Ok(client);
    }

    public async Task<Result<List<string>>> ListCorsOrigins(
        ListCorsOrigins.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            ListCorsOriginsSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);

        var origins = await connectionWrapper.QueryAsync<string>(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);

        return origins.ToList();
    }

    public async Task<Result<VerifySecret.Item?>> VerifySecret(
        VerifySecret.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            VerifySecretSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);
        queryParameters.Add(nameof(query.ClientId), query.ClientId, DbType.String);

        await using var grid = await connectionWrapper.QueryMultipleAsync(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);
        var client = await grid.ReadSingleOrDefaultAsync<VerifySecret.Item>();
        var secrets = (await grid.ReadAsync<VerifySecret.Secret>()).ToArray();

        if (client is null) return Result.Ok<VerifySecret.Item?>(null);

        client.Secrets = secrets;
        return Result.Ok<VerifySecret.Item?>(client);
    }
}
