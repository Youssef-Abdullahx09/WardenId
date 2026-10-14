using FluentResults;
using MediatR;
using WardenId.Protocol.Query.Contracts;
using SessionsQuery = WardenId.Protocol.Query.Contracts.Features.Sessions.ListForUser.Query;

namespace WardenId.Protocol.Application.Features.Sessions.ListForUser;

/// <summary>Read side: live sessions come from the Dapper query layer (Protocol.Query), not the aggregate.</summary>
public sealed class Handler(
    ISessionsQuery sessionsQuery,
    TimeProvider timeProvider) : IRequestHandler<Query, Result<IReadOnlyList<UserSession>>>
{
    public async Task<Result<IReadOnlyList<UserSession>>> Handle(Query request, CancellationToken cancellationToken)
    {
        var result = await sessionsQuery.ListForUser(
            new SessionsQuery(request.TenantId, request.UserId, timeProvider.GetUtcNow()),
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        IReadOnlyList<UserSession> sessions = result.Value
            .Select(item => new UserSession(
                item.Sid,
                AsUtc(item.AuthTime),
                AsUtc(item.ExpiresAt),
                item.ClientIds))
            .ToList();

        return Result.Ok(sessions);
    }

    // Npgsql materializes timestamptz as DateTime with Kind=Utc.
    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);
}
