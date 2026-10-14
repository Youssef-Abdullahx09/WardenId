using Dapper;
using FluentResults;
using WardenId.Protocol.Query.Contracts;
using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Query;

namespace WardenId.Protocol.Query;

public sealed class SessionsQuery(
    IConnectionWrapper connectionWrapper,
    IReadFileService readFileService) : ISessionsQuery
{
    public async Task<Result<List<Contracts.Features.Sessions.ListForUser.Item>>> ListForUser(
        Contracts.Features.Sessions.ListForUser.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(
            "/Features/Sessions/ListForUser/Query.sql",
            cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);
        queryParameters.Add(nameof(query.UserId), query.UserId);
        queryParameters.Add(nameof(query.Now), query.Now);

        var sessions = await connectionWrapper.QueryAsync<Contracts.Features.Sessions.ListForUser.Item>(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);

        return sessions.ToList();
    }
}
