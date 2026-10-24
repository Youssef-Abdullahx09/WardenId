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
    private const string GetLiveSql = "/Features/Sessions/GetLive/Query.sql";
    private const string OffsetParameter = "Offset";
    private const string DescendingParameter = "Descending";

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
        // A blank client_id filters nothing.
        queryParameters.Add(
            nameof(query.ClientId),
            string.IsNullOrWhiteSpace(query.ClientId) ? null : query.ClientId,
            DbType.String);
        queryParameters.Add(nameof(query.IncludeEnded), query.IncludeEnded);
        queryParameters.Add(nameof(query.Now), query.Now);
        // Matched by member name in Query.sql; Dapper would otherwise send the enum as its number.
        queryParameters.Add(nameof(query.SortOption), query.SortOption.ToString(), DbType.String);
        queryParameters.Add(DescendingParameter, query.SortDirection == SortDirection.Desc);
        queryParameters.Add(nameof(query.PageSize), query.PageSize);
        queryParameters.Add(OffsetParameter, (query.PageNumber - 1) * query.PageSize);

        long recordsTotal = 0;

        var sessions = await connectionWrapper
            .QueryAsync<Contracts.Features.Sessions.List.Item, long, Contracts.Features.Sessions.List.Item>(
                readingFileResult.Value,
                (item, count) =>
                {
                    recordsTotal = count;
                    return item;
                },
                splitOn: nameof(PaginationListResponse<Contracts.Features.Sessions.List.Item>.RecordsTotal),
                param: queryParameters,
                cancellationToken: cancellationToken);

        return new PaginationListResponse<Contracts.Features.Sessions.List.Item>(recordsTotal, sessions.ToList());
    }

    public async Task<Result<Contracts.Features.Sessions.GetLive.Item?>> GetLive(
        Contracts.Features.Sessions.GetLive.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            GetLiveSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);
        queryParameters.Add(nameof(query.Sid), query.Sid);

        var session = await connectionWrapper.QueryFirstOrDefaultAsync<Contracts.Features.Sessions.GetLive.Item>(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);

        return Result.Ok(session);
    }
}
