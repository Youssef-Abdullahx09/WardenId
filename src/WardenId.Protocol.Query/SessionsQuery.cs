using System.Data;
using Dapper;
using FluentResults;
using WardenId.Protocol.Query.Contracts;
using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Query;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Protocol.Query;

public sealed class SessionsQuery(
    IConnectionWrapper connectionWrapper,
    IReadFileService readFileService) : ISessionsQuery
{
    private const string ListForUserSql = "/Features/Sessions/ListForUser/Query.sql";
    private const string ListSql = "/Features/Sessions/List/Query.sql";
    private const string OffsetParameter = "Offset";

    public async Task<Result<List<Contracts.Features.Sessions.ListForUser.Item>>> ListForUser(
        Contracts.Features.Sessions.ListForUser.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            ListForUserSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);
        queryParameters.Add(nameof(query.UserId), query.UserId);
        queryParameters.Add(nameof(query.Now), query.Now);
        queryParameters.Add(nameof(query.IncludeEnded), query.IncludeEnded);

        var sessions = await connectionWrapper.QueryAsync<Contracts.Features.Sessions.ListForUser.Item>(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);

        return sessions.ToList();
    }

    public async Task<Result<PaginationListResponse<Contracts.Features.Sessions.List.Item>>> List(
        Contracts.Features.Sessions.List.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            ListSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);
        // Typed explicitly: an untyped NULL leaves Postgres unable to infer "@UserId IS NULL".
        queryParameters.Add(nameof(query.UserId), query.UserId, DbType.Guid);
        queryParameters.Add(nameof(query.ClientId), query.ClientId, DbType.String);
        queryParameters.Add(nameof(query.IncludeEnded), query.IncludeEnded);
        queryParameters.Add(nameof(query.Now), query.Now);
        queryParameters.Add(nameof(query.PageSize), query.PageSize);
        queryParameters.Add(OffsetParameter, (query.PageNumber - 1) * query.PageSize);

        await using var grid = await connectionWrapper.QueryMultipleAsync(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);
        var total = await grid.ReadSingleAsync<long>();
        var sessions = (await grid.ReadAsync<Contracts.Features.Sessions.List.Item>()).ToList();

        return new PaginationListResponse<Contracts.Features.Sessions.List.Item>(total, sessions);
    }
}
