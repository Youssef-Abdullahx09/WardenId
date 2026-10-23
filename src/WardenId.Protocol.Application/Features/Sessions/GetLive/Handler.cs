using FluentResults;
using MediatR;
using WardenId.Protocol.Domain;
using WardenId.Protocol.Query.Contracts;
using GetLiveSessionQuery = WardenId.Protocol.Query.Contracts.Features.Sessions.GetLive.Query;

namespace WardenId.Protocol.Application.Features.Sessions.GetLive;

/// <summary>Read side: the session comes from the Dapper query layer (Protocol.Query), not the aggregate.</summary>
public sealed class Handler(
    ISessionsQuery sessionsQuery,
    TimeProvider timeProvider) : IRequestHandler<Query, Result<LiveSession>>
{
    public async Task<Result<LiveSession>> Handle(Query request, CancellationToken cancellationToken)
    {
        var result = await sessionsQuery.GetLive(new GetLiveSessionQuery(request.TenantId, request.Sid), cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        // Missing, other tenant, ended and expired are indistinguishable to the caller. Same rule as Session.IsLive.
        var session = result.Value;
        if (session is null || session.EndedAt is not null
            || timeProvider.GetUtcNow() >= SessionTimes.AsUtc(session.ExpiresAt))
            return Result.Fail(SessionErrors.NotFound());

        return Result.Ok(new LiveSession(session.Sid, session.UserId, SessionTimes.AsUtc(session.AuthTime)));
    }
}
