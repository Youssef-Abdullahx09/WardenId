using Microsoft.Extensions.Time.Testing;
using WardenId.Protocol.Application.Features.Sessions.GetLive;
using GetLiveQuery = WardenId.Protocol.Application.Features.Sessions.GetLive.Query;
using Item = WardenId.Protocol.Query.Contracts.Features.Sessions.GetLive.Item;

namespace WardenId.Protocol.Application.Tests.Features.Sessions.GetLive;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = SessionBuilder.Now.UtcDateTime;

    private static Item Row(Guid sid, DateTime? expiresAt = null, DateTime? endedAt = null) => new()
    {
        Sid = sid,
        UserId = UserId,
        AuthTime = Now.AddHours(-1),
        ExpiresAt = expiresAt ?? Now.AddDays(1),
        EndedAt = endedAt
    };

    private static async Task<Result<LiveSession>> HandleAsync(FakeSessionsQuery query, Guid tenantId, Guid sid) =>
        await new Handler(query, new FakeTimeProvider(SessionBuilder.Now))
            .Handle(new GetLiveQuery(tenantId, sid), CancellationToken.None);

    private static void AssertNotFound(ResultBase result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.NotFound, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task LiveSession_ReturnsSidUserAndAuthTime_AsUtc()
    {
        var sid = Guid.NewGuid();
        var query = new FakeSessionsQuery { GetLiveResponse = Result.Ok<Item?>(Row(sid)) };

        var result = await HandleAsync(query, TenantId, sid);

        Assert.True(result.IsSuccess);
        Assert.Equal(new LiveSession(sid, UserId, SessionBuilder.Now.AddHours(-1)), result.Value);
        Assert.Equal(TimeSpan.Zero, result.Value.AuthTime.Offset);
    }

    [Fact]
    public async Task PassesTenantAndSidToTheQuery()
    {
        var sid = Guid.NewGuid();
        var query = new FakeSessionsQuery();

        await HandleAsync(query, TenantId, sid);

        var call = Assert.Single(query.GetLiveCalls);
        Assert.Equal(TenantId, call.TenantId);
        Assert.Equal(sid, call.Sid);
    }

    [Fact]
    public async Task NoRow_IsNotFound()
    {
        // Unknown sid and other tenant both come back as no row.
        AssertNotFound(await HandleAsync(new FakeSessionsQuery(), TenantId, Guid.NewGuid()));
    }

    [Fact]
    public async Task EndedSession_IsNotFound()
    {
        var sid = Guid.NewGuid();
        var query = new FakeSessionsQuery { GetLiveResponse = Result.Ok<Item?>(Row(sid, endedAt: Now.AddMinutes(-10))) };

        AssertNotFound(await HandleAsync(query, TenantId, sid));
    }

    [Fact]
    public async Task ExpiredSession_IsNotFound()
    {
        var sid = Guid.NewGuid();
        var query = new FakeSessionsQuery { GetLiveResponse = Result.Ok<Item?>(Row(sid, expiresAt: Now.AddDays(-1))) };

        AssertNotFound(await HandleAsync(query, TenantId, sid));
    }

    [Fact]
    public async Task SessionExpiringExactlyNow_IsNotFound()
    {
        var sid = Guid.NewGuid();
        var query = new FakeSessionsQuery { GetLiveResponse = Result.Ok<Item?>(Row(sid, expiresAt: Now)) };

        AssertNotFound(await HandleAsync(query, TenantId, sid));
    }

    [Fact]
    public async Task QueryFailure_IsPassedThrough()
    {
        var query = new FakeSessionsQuery { GetLiveResponse = Result.Fail<Item?>("file missing") };

        var result = await HandleAsync(query, TenantId, Guid.NewGuid());

        Assert.True(result.IsFailed);
        Assert.Equal("file missing", result.Errors[0].Message);
    }
}
