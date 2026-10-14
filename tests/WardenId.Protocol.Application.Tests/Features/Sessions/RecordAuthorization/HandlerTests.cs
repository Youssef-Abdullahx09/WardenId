using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using WardenId.Protocol.Application.Features.Sessions.RecordAuthorization;
using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Tests.Features.Sessions.RecordAuthorization;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static async Task<Result> HandleAsync(
        string databaseName, Guid tenantId, Guid sid, string authorizationId = "auth-1", bool offlineAccess = false)
    {
        await using var db = TestProtocolDbContext.CreateNew(databaseName);
        return await new Handler(db, new FakeTimeProvider(SessionBuilder.Now))
            .Handle(new Command(tenantId, sid, authorizationId, "web", offlineAccess), CancellationToken.None);
    }

    private static async Task<IReadOnlyCollection<SessionAuthorization>> AuthorizationsAsync(
        string databaseName, Guid sid)
    {
        await using var db = TestProtocolDbContext.CreateNew(databaseName);
        return (await db.Sessions.SingleAsync(s => s.Id == sid)).Authorizations;
    }

    private static void AssertErrorCode(string expected, Result result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(expected, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task LiveSession_RecordsAuthorization()
    {
        var session = SessionBuilder.Live(TenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(session);

        var result = await HandleAsync(databaseName, TenantId, session.Id, offlineAccess: true);

        Assert.True(result.IsSuccess);
        var authorization = Assert.Single(await AuthorizationsAsync(databaseName, session.Id));
        Assert.Equal("auth-1", authorization.AuthorizationId);
        Assert.Equal("web", authorization.ClientId);
        Assert.True(authorization.OfflineAccess);
        Assert.Equal(SessionBuilder.Now, authorization.CreatedAt);
    }

    [Fact]
    public async Task SameAuthorizationTwice_IsStoredOnce()
    {
        var session = SessionBuilder.Live(TenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(session);

        await HandleAsync(databaseName, TenantId, session.Id);
        var second = await HandleAsync(databaseName, TenantId, session.Id);

        Assert.True(second.IsSuccess);
        Assert.Single(await AuthorizationsAsync(databaseName, session.Id));
    }

    [Fact]
    public async Task EndedSession_FailsSessionExpired()
    {
        var session = SessionBuilder.Ended(TenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(session);

        AssertErrorCode(ProtocolErrorCodes.SessionExpired, await HandleAsync(databaseName, TenantId, session.Id));
        Assert.Empty(await AuthorizationsAsync(databaseName, session.Id));
    }

    [Fact]
    public async Task UnknownSid_IsNotFound()
    {
        var databaseName = await SessionBuilder.SeedAsync(SessionBuilder.Live(TenantId, UserId));

        AssertErrorCode(ProtocolErrorCodes.NotFound, await HandleAsync(databaseName, TenantId, Guid.NewGuid()));
    }

    [Fact]
    public async Task OtherTenant_IsNotFound_AndRecordsNothing()
    {
        var session = SessionBuilder.Live(TenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(session);

        AssertErrorCode(ProtocolErrorCodes.NotFound, await HandleAsync(databaseName, Guid.NewGuid(), session.Id));
        Assert.Empty(await AuthorizationsAsync(databaseName, session.Id));
    }
}
