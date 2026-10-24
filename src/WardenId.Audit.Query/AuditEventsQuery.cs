using System.Data;
using Dapper;
using FluentResults;
using WardenId.Audit.Query.Contracts;
using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Query;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Audit.Query;

public sealed class AuditEventsQuery(
    IConnectionWrapper connectionWrapper,
    IReadFileService readFileService) : IAuditEventsQuery
{
    private const string ListSql = "/Features/AuditEvents/List/Query.sql";
    private const string GetSql = "/Features/AuditEvents/Get/Query.sql";
    private const string OffsetParameter = "Offset";
    private const string DescendingParameter = "Descending";

    public async Task<Result<PaginationListResponse<Contracts.Features.AuditEvents.List.Item>>> List(
        Contracts.Features.AuditEvents.List.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            ListSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        // Typed explicitly: an untyped NULL leaves Postgres unable to infer "@X IS NULL".
        // Enums are persisted by member name; Dapper would otherwise send them as their numbers.
        queryParameters.Add(nameof(query.Channel), query.Channel?.ToString(), DbType.String);
        queryParameters.Add(nameof(query.TenantId), query.TenantId, DbType.Guid);
        // Npgsql only writes offset-0 DateTimeOffsets to timestamptz; the instant is unchanged.
        queryParameters.Add(nameof(query.From), query.From?.ToUniversalTime(), DbType.DateTimeOffset);
        queryParameters.Add(nameof(query.To), query.To?.ToUniversalTime(), DbType.DateTimeOffset);
        queryParameters.Add(nameof(query.EventType), TrimmedOrNull(query.EventType), DbType.String);
        queryParameters.Add(nameof(query.Actor), TrimmedOrNull(query.Actor), DbType.String);
        queryParameters.Add(nameof(query.Target), TrimmedOrNull(query.Target), DbType.String);
        queryParameters.Add(nameof(query.Outcome), query.Outcome?.ToString(), DbType.String);
        queryParameters.Add(nameof(query.SortOption), query.SortOption.ToString(), DbType.String);
        queryParameters.Add(DescendingParameter, query.SortDirection == SortDirection.Desc);
        queryParameters.Add(nameof(query.PageSize), query.PageSize);
        queryParameters.Add(OffsetParameter, (query.PageNumber - 1) * query.PageSize);

        long recordsTotal = 0;

        var events = await connectionWrapper
            .QueryAsync<Contracts.Features.AuditEvents.List.Item, long, Contracts.Features.AuditEvents.List.Item>(
                readingFileResult.Value,
                (item, count) =>
                {
                    recordsTotal = count;
                    return item;
                },
                splitOn: nameof(PaginationListResponse<Contracts.Features.AuditEvents.List.Item>.RecordsTotal),
                param: queryParameters,
                cancellationToken: cancellationToken);

        return new PaginationListResponse<Contracts.Features.AuditEvents.List.Item>(recordsTotal, events.ToList());
    }

    public async Task<Result<Contracts.Features.AuditEvents.Get.Item?>> Get(
        Contracts.Features.AuditEvents.Get.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            GetSql,
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.EventId), query.EventId);

        var events = await connectionWrapper.QueryAsync<Contracts.Features.AuditEvents.Get.Item>(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);

        return Result.Ok(events.FirstOrDefault());
    }

    // The text filters are exact matches, so surrounding whitespace is dropped and a blank one means no filter.
    private static string? TrimmedOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
