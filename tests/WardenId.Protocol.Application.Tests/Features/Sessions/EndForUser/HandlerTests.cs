using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using WardenId.Protocol.Application.Features.Sessions.EndForUser;
using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Tests.Features.Sessions.EndForUser;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static async Task<Result> HandleAsync(string databaseName, EndReason reason, Guid? tenantId)
    {
        await using var db = TestProtocolDbContext.CreateNew(databaseName);
        return await new Handler(db, new FakeTimeProvider(SessionBuilder.Now))
            .Handle(new Command(UserId, reason, tenantId), CancellationToken.None);
    }

    private static async Task<Session> LoadAsync(string databaseName, Guid sid)
    {
        await using var db = TestProtocolDbContext.CreateNew(databaseName);
        return await db.Sessions.SingleAsync(s => s.Id == sid);
    }

    // Same user on two browsers/tenants: a password change must end both; other users stay signed in.
    [Fact]
    public async Task EndForUser_EndsAllUnendedSessions()
    {
        var first = SessionBuilder.Live(TenantId, UserId);
        var second = SessionBuilder.Live(OtherTenantId, UserId);
        var alreadyEnded = SessionBuilder.Ended(TenantId, UserId, EndReason.UserLogout);
        var otherUser = SessionBuilder.Live(TenantId, Guid.NewGuid());
        var databaseName = await SessionBuilder.SeedAsync(first, second, alreadyEnded, otherUser);

        var result = await HandleAsync(databaseName, EndReason.PasswordChanged, tenantId: null);

        Assert.True(result.IsSuccess);
        foreach (var sid in new[] { first.Id, second.Id })
        {
            var stored = await LoadAsync(databaseName, sid);
            Assert.Equal(SessionBuilder.Now, stored.EndedAt);
            Assert.Equal(EndReason.PasswordChanged, stored.EndReason);
        }

        var untouchedEnded = await LoadAsync(databaseName, alreadyEnded.Id);
        Assert.Equal(EndReason.UserLogout, untouchedEnded.EndReason);
        Assert.Equal(SessionBuilder.Now.AddMinutes(-10), untouchedEnded.EndedAt);
        Assert.Null((await LoadAsync(databaseName, otherUser.Id)).EndedAt);
    }

    [Fact]
    public async Task EndForUser_WithTenant_EndsOnlyThatTenantsSessions()
    {
        var inTenant = SessionBuilder.Live(TenantId, UserId);
        var inOtherTenant = SessionBuilder.Live(OtherTenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(inTenant, inOtherTenant);

        var result = await HandleAsync(databaseName, EndReason.AdminEnded, TenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(EndReason.AdminEnded, (await LoadAsync(databaseName, inTenant.Id)).EndReason);
        Assert.Null((await LoadAsync(databaseName, inOtherTenant.Id)).EndedAt);
    }

    [Fact]
    public async Task EndForUser_EndsExpiredButUnendedSessions()
    {
        var expired = SessionBuilder.Expired(TenantId, UserId);
        var databaseName = await SessionBuilder.SeedAsync(expired);

        var result = await HandleAsync(databaseName, EndReason.UserDisabled, tenantId: null);

        Assert.True(result.IsSuccess);
        var stored = await LoadAsync(databaseName, expired.Id);
        Assert.Equal(SessionBuilder.Now, stored.EndedAt);
        Assert.Equal(EndReason.UserDisabled, stored.EndReason);
    }

    [Fact]
    public async Task EndForUser_WithNoUnendedSessions_Succeeds_AndChangesNothing()
    {
        var alreadyEnded = SessionBuilder.Ended(TenantId, UserId, EndReason.UserLogout);
        var databaseName = await SessionBuilder.SeedAsync(alreadyEnded);

        var result = await HandleAsync(databaseName, EndReason.PasswordChanged, tenantId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(EndReason.UserLogout, (await LoadAsync(databaseName, alreadyEnded.Id)).EndReason);
    }
}
