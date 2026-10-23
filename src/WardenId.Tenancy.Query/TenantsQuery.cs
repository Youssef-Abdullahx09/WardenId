using System.Data;
using Dapper;
using FluentResults;
using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Query;
using WardenId.SharedKernel.Query.Contracts;
using WardenId.Tenancy.Query.Contracts;

namespace WardenId.Tenancy.Query;

public sealed class TenantsQuery(
    IConnectionWrapper connectionWrapper,
    IReadFileService readFileService) : ITenantsQuery
{
    private const string GetSql = "/Features/Tenants/Get/Query.sql";
    private const string GetBySlugSql = "/Features/Tenants/GetBySlug/Query.sql";
    private const string ListSql = "/Features/Tenants/List/Query.sql";
    private const string ResolveForIssuanceSql = "/Features/Tenants/ResolveForIssuance/Query.sql";
    private const string OffsetParameter = "Offset";

    public async Task<Result<Contracts.Features.Tenants.Get.Item?>> Get(
        Contracts.Features.Tenants.Get.Query query,
        CancellationToken cancellationToken)
    {
        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);

        return await ReadTenantWithKeys(GetSql, queryParameters, cancellationToken);
    }

    public async Task<Result<Contracts.Features.Tenants.Get.Item?>> GetBySlug(
        Contracts.Features.Tenants.GetBySlug.Query query,
        CancellationToken cancellationToken)
    {
        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.Slug), query.Slug, DbType.String);

        return await ReadTenantWithKeys(GetBySlugSql, queryParameters, cancellationToken);
    }

    public async Task<Result<PaginationListResponse<Contracts.Features.Tenants.List.Item>>> List(
        Contracts.Features.Tenants.List.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            ListSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        // Typed explicitly: an untyped NULL leaves Postgres unable to infer "@Status IS NULL".
        queryParameters.Add(nameof(query.Status), query.Status, DbType.String);
        queryParameters.Add(nameof(query.PageSize), query.PageSize);
        queryParameters.Add(OffsetParameter, (query.PageNumber - 1) * query.PageSize);

        await using var grid = await connectionWrapper.QueryMultipleAsync(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);
        var total = await grid.ReadSingleAsync<long>();
        var tenants = (await grid.ReadAsync<Contracts.Features.Tenants.List.Item>()).ToList();

        return new PaginationListResponse<Contracts.Features.Tenants.List.Item>(total, tenants);
    }

    public async Task<Result<Contracts.Features.Tenants.ResolveForIssuance.Item?>> ResolveForIssuance(
        Contracts.Features.Tenants.ResolveForIssuance.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            ResolveForIssuanceSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.Slug), query.Slug, DbType.String);

        var item = await connectionWrapper.QueryFirstOrDefaultAsync<Contracts.Features.Tenants.ResolveForIssuance.Item>(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);

        return Result.Ok<Contracts.Features.Tenants.ResolveForIssuance.Item?>(item);
    }

    private async Task<Result<Contracts.Features.Tenants.Get.Item?>> ReadTenantWithKeys(
        string sqlPath,
        DynamicParameters queryParameters,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            sqlPath,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        await using var grid = await connectionWrapper.QueryMultipleAsync(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);
        var tenant = await grid.ReadSingleOrDefaultAsync<Contracts.Features.Tenants.Get.Item>();
        var signingKeys = (await grid.ReadAsync<Contracts.Features.Tenants.Get.SigningKeyItem>()).ToList();
        if (tenant is null) return Result.Ok<Contracts.Features.Tenants.Get.Item?>(null);

        tenant.SigningKeys = signingKeys;
        return Result.Ok<Contracts.Features.Tenants.Get.Item?>(tenant);
    }
}
