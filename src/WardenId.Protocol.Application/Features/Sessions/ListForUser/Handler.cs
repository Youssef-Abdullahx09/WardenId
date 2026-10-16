using FluentResults;
using MediatR;
using WardenId.Protocol.Query.Contracts;
using SessionsQuery = WardenId.Protocol.Query.Contracts.Features.Sessions.ListForUser.Query;

namespace WardenId.Protocol.Application.Features.Sessions.ListForUser;

/// <summary>Read side: a user's sessions come from the Dapper query layer (Protocol.Query), not the aggregate.</summary>
public sealed class Handler(
    ISessionsQuery sessionsQuery,
    TimeProvider timeProvider) : IRequestHandler<Query, Result<IReadOnlyList<UserSession>>>
{
    public async Task<Result<IReadOnlyList<UserSession>>> Handle(Query request, CancellationToken cancellationToken)
    {
        var result = await sessionsQuery.ListForUser(
            new SessionsQuery(request.TenantId, request.UserId, timeProvider.GetUtcNow(), request.IncludeEnded),
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        IReadOnlyList<UserSession> sessions = result.Value
            .Select(item => new UserSession(
                item.Sid,
                SessionTimes.AsUtc(item.AuthTime),
                SessionTimes.AsUtc(item.ExpiresAt),
                item.ClientIds,
                SessionTimes.AsUtc(item.EndedAt),
                SessionTimes.ParseEndReason(item.EndReason)))
            .ToList();

        return Result.Ok(sessions);
    }
}
