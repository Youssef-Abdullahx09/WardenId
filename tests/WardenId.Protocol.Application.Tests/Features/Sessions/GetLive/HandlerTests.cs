using Microsoft.Extensions.Time.Testing;
using WardenId.Protocol.Application.Features.Sessions.GetLive;
using GetLiveQuery = WardenId.Protocol.Application.Features.Sessions.GetLive.Query;

namespace WardenId.Protocol.Application.Tests.Features.Sessions.GetLive;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static async Task<Result<LiveSession>> HandleAsync(string databaseName, Guid tenantId, Guid sid)
    {
        await using var db = TestProtocolDbContext.CreateNew(databaseName);
        return await new Handler(db, new FakeTimeProvider(SessionBuilder.Now))
            .Handle(new GetLiveQuery(tenantId, sid), CancellationToken.None);
    }

    private static void AssertNotFound(ResultBase result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.NotFound, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task LiveSession_ReturnsSidUserAndAuthTime()
    {
        var session = SessionBuilder.Live(TenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(session);

        var result = await HandleAsync(databaseName, TenantId, session.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(new LiveSession(session.Id, UserId, session.AuthTime), result.Value);
    }

    [Fact]
    public async Task UnknownSid_IsNotFound()
    {
        var databaseName = await SessionBuilder.SeedAsync(SessionBuilder.Live(TenantId, UserId));

        AssertNotFound(await HandleAsync(databaseName, TenantId, Guid.NewGuid()));
    }

    [Fact]
    public async Task OtherTenant_IsNotFound()
    {
        var session = SessionBuilder.Live(TenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(session);

        AssertNotFound(await HandleAsync(databaseName, Guid.NewGuid(), session.Id));
    }

    [Fact]
    public async Task EndedSession_IsNotFound()
    {
        var session = SessionBuilder.Ended(TenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(session);

        AssertNotFound(await HandleAsync(databaseName, TenantId, session.Id));
    }

    [Fact]
    public async Task ExpiredSession_IsNotFound()
    {
        var session = SessionBuilder.Expired(TenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(session);

        AssertNotFound(await HandleAsync(databaseName, TenantId, session.Id));
    }
}
