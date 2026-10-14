using Microsoft.Extensions.Time.Testing;
using WardenId.Protocol.Application.Features.Sessions.ListForUser;
using ListForUserQuery = WardenId.Protocol.Application.Features.Sessions.ListForUser.Query;
using QueryItem = WardenId.Protocol.Query.Contracts.Features.Sessions.ListForUser.Item;
using SessionsQuery = WardenId.Protocol.Query.Contracts.Features.Sessions.ListForUser.Query;

namespace WardenId.Protocol.Application.Tests.Features.Sessions.ListForUser;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static Task<Result<IReadOnlyList<UserSession>>> HandleAsync(FakeSessionsQuery sessionsQuery) =>
        new Handler(sessionsQuery, new FakeTimeProvider(Now))
            .Handle(new ListForUserQuery(TenantId, UserId), CancellationToken.None);

    [Fact]
    public async Task PassesTenantUserAndCurrentTime_ToTheQueryLayer()
    {
        var sessionsQuery = new FakeSessionsQuery();

        await HandleAsync(sessionsQuery);

        Assert.Equal(new SessionsQuery(TenantId, UserId, Now), Assert.Single(sessionsQuery.ListForUserCalls));
    }

    [Fact]
    public async Task MapsRows_ToUserSessions_InQueryOrder_WithUtcTimes()
    {
        var newer = Guid.NewGuid();
        var older = Guid.NewGuid();
        var sessionsQuery = new FakeSessionsQuery
        {
            ListForUserResponse = Result.Ok(new List<QueryItem>
            {
                new()
                {
                    Sid = newer,
                    AuthTime = Now.AddHours(-1).UtcDateTime,
                    ExpiresAt = Now.AddHours(23).UtcDateTime
                },
                new()
                {
                    Sid = older,
                    AuthTime = Now.AddHours(-3).UtcDateTime,
                    ExpiresAt = Now.AddHours(21).UtcDateTime,
                    ClientIds = ["spa", "web"]
                }
            })
        };

        var result = await HandleAsync(sessionsQuery);

        Assert.True(result.IsSuccess);
        Assert.Collection(
            result.Value,
            s =>
            {
                Assert.Equal(newer, s.Sid);
                Assert.Equal(Now.AddHours(-1), s.AuthTime);
                Assert.Equal(TimeSpan.Zero, s.AuthTime.Offset);
                Assert.Equal(Now.AddHours(23), s.ExpiresAt);
                Assert.Empty(s.ClientIds);
            },
            s =>
            {
                Assert.Equal(older, s.Sid);
                Assert.Equal(new[] { "spa", "web" }, s.ClientIds);
            });
    }

    [Fact]
    public async Task NoRows_ReturnsEmpty()
    {
        var result = await HandleAsync(new FakeSessionsQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task QueryFailure_IsPropagated()
    {
        var failure = new Error("File not found !");
        var sessionsQuery = new FakeSessionsQuery { ListForUserResponse = Result.Fail(failure) };

        var result = await HandleAsync(sessionsQuery);

        Assert.True(result.IsFailed);
        Assert.Same(failure, result.Errors[0]);
    }
}
