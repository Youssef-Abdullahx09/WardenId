using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using WardenId.Protocol.Application.Features.Sessions.End;
using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Tests.Features.Sessions.End;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static async Task<Result> HandleAsync(
        string databaseName, Guid tenantId, Guid sid, EndReason reason, bool requireExisting)
    {
        await using var db = TestProtocolDbContext.CreateNew(databaseName);
        return await new Handler(db, new FakeTimeProvider(SessionBuilder.Now))
            .Handle(new Command(tenantId, sid, reason, requireExisting), CancellationToken.None);
    }

    private static async Task<Session> LoadAsync(string databaseName, Guid sid)
    {
        await using var db = TestProtocolDbContext.CreateNew(databaseName);
        return await db.Sessions.SingleAsync(s => s.Id == sid);
    }

    private static void AssertNotFound(Result result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.NotFound, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task LiveSession_IsEndedWithReason()
    {
        var session = SessionBuilder.Live(TenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(session);

        var result = await HandleAsync(databaseName, TenantId, session.Id, EndReason.AdminEnded, requireExisting: true);

        Assert.True(result.IsSuccess);
        var stored = await LoadAsync(databaseName, session.Id);
        Assert.Equal(SessionBuilder.Now, stored.EndedAt);
        Assert.Equal(EndReason.AdminEnded, stored.EndReason);
    }

    [Fact]
    public async Task UnknownSid_RequireExisting_IsNotFound()
    {
        var databaseName = await SessionBuilder.SeedAsync();

        AssertNotFound(await HandleAsync(databaseName, TenantId, Guid.NewGuid(), EndReason.AdminEnded, requireExisting: true));
    }

    [Fact]
    public async Task UnknownSid_NotRequired_IsOk()
    {
        var databaseName = await SessionBuilder.SeedAsync();

        var result = await HandleAsync(databaseName, TenantId, Guid.NewGuid(), EndReason.UserLogout, requireExisting: false);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AlreadyEnded_RequireExisting_IsNotFound()
    {
        var session = SessionBuilder.Ended(TenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(session);

        AssertNotFound(await HandleAsync(databaseName, TenantId, session.Id, EndReason.AdminEnded, requireExisting: true));
    }

    [Fact]
    public async Task AlreadyEnded_NotRequired_IsOk_AndKeepsFirstReason()
    {
        var session = SessionBuilder.Ended(TenantId, UserId, EndReason.UserLogout);
        var databaseName = await SessionBuilder.SeedAsync(session);

        var result = await HandleAsync(databaseName, TenantId, session.Id, EndReason.PasswordChanged, requireExisting: false);

        Assert.True(result.IsSuccess);
        var stored = await LoadAsync(databaseName, session.Id);
        Assert.Equal(EndReason.UserLogout, stored.EndReason);
        Assert.Equal(SessionBuilder.Now.AddMinutes(-10), stored.EndedAt);
    }

    [Fact]
    public async Task OtherTenant_RequireExisting_IsNotFound_AndSessionStaysLive()
    {
        var session = SessionBuilder.Live(TenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(session);

        AssertNotFound(await HandleAsync(databaseName, Guid.NewGuid(), session.Id, EndReason.AdminEnded, requireExisting: true));
        Assert.Null((await LoadAsync(databaseName, session.Id)).EndedAt);
    }
}
