using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Features.Policies.UpdateTokens;
using WardenId.Protocol.Domain.TokenPolicies;
using Command = WardenId.Protocol.Application.Features.Policies.UpdateTokens.Command;

namespace WardenId.Protocol.Application.Tests.Features.Policies.UpdateTokens;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static Handler CreateHandler(
        TestProtocolDbContext db, FakeTenantLifetimes? lifetimes = null, TimeSpan? retention = null,
        ProtocolTokenOptions? tokens = null, ProtocolSessionOptions? sessions = null) =>
        new(db, lifetimes ?? new FakeTenantLifetimes(),
            Options.Create(tokens ?? new ProtocolTokenOptions()),
            Options.Create(sessions ?? new ProtocolSessionOptions()),
            new FakeRefreshTokenRetention(retention ?? TimeSpan.FromDays(90)));

    private static Command Command(
        TimeSpan? accessToken = null, TimeSpan? idToken = null, TimeSpan? refreshToken = null,
        TimeSpan? refreshTokenAbsolute = null, TimeSpan? authorizationCode = null,
        TimeSpan? sessionCookie = null, TimeSpan? sessionAbsolute = null) =>
        new(TenantId, accessToken, idToken, refreshToken, refreshTokenAbsolute, authorizationCode, sessionCookie,
            sessionAbsolute, ActorId);

    private static string ErrorCode(ResultBase result) => (string)result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey];

    [Fact]
    public async Task Handle_FirstSave_CreatesThePolicy_AndInvalidatesTheCache()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var db = TestProtocolDbContext.CreateNew(databaseName);
        var lifetimes = new FakeTenantLifetimes();

        var result = await CreateHandler(db, lifetimes).Handle(
            Command(accessToken: TimeSpan.FromMinutes(5), sessionCookie: TimeSpan.FromHours(1)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var check = TestProtocolDbContext.CreateNew(databaseName);
        var policy = await check.TenantTokenPolicies.SingleAsync();
        Assert.Equal(TenantId, policy.TenantId);
        Assert.Equal(TimeSpan.FromMinutes(5), policy.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromHours(1), policy.SessionCookieLifetime);
        Assert.Null(policy.IdTokenLifetime);
        Assert.Equal([TenantId], lifetimes.Invalidated);
    }

    [Fact]
    public async Task Handle_SecondSave_ReplacesTheSameRow_NullsClear()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using (var db = TestProtocolDbContext.CreateNew(databaseName))
            await CreateHandler(db).Handle(Command(accessToken: TimeSpan.FromMinutes(5)), CancellationToken.None);

        await using (var db = TestProtocolDbContext.CreateNew(databaseName))
        {
            var result = await CreateHandler(db).Handle(Command(idToken: TimeSpan.FromMinutes(2)), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        await using var check = TestProtocolDbContext.CreateNew(databaseName);
        var policy = await check.TenantTokenPolicies.SingleAsync();
        Assert.Null(policy.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromMinutes(2), policy.IdTokenLifetime);
    }

    [Fact]
    public async Task Handle_OutOfBounds_FailsValidation_AndSavesNothing()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());
        var lifetimes = new FakeTenantLifetimes();

        var result = await CreateHandler(db, lifetimes).Handle(Command(accessToken: TimeSpan.FromDays(2)), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.Validation, ErrorCode(result));
        Assert.Empty(db.TenantTokenPolicies);
        Assert.Empty(lifetimes.Invalidated);
    }

    [Fact]
    public async Task Handle_TenantRefreshLongerThanGlobalAbsoluteCap_FailsValidation()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());

        // Global absolute cap is 90 days; a 120-day sliding lifetime could never be reached.
        var result = await CreateHandler(db, retention: TimeSpan.FromDays(365))
            .Handle(Command(refreshToken: TimeSpan.FromDays(120)), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.StartsWith(TenantLifetimeOverrides.RefreshTokenAbsoluteField, result.Errors[0].Message);
    }

    [Fact]
    public async Task Handle_TenantCookieLongerThanGlobalSessionCap_FailsValidation()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());

        var result = await CreateHandler(db).Handle(Command(sessionCookie: TimeSpan.FromDays(2)), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.StartsWith(TenantLifetimeOverrides.SessionAbsoluteField, result.Errors[0].Message);
    }

    [Fact]
    public async Task Handle_AbsoluteCapBeyondRefreshTokenRetention_FailsValidation()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());

        var result = await CreateHandler(db, retention: TimeSpan.FromDays(90))
            .Handle(Command(refreshTokenAbsolute: TimeSpan.FromDays(180)), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.Validation, ErrorCode(result));
        Assert.StartsWith(TenantLifetimeOverrides.RefreshTokenAbsoluteField, result.Errors[0].Message);
    }

    [Fact]
    public async Task Handle_AbsoluteCapBeyondRetention_IsAllowed_WhenPruningIsOff()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());
        var handler = new Handler(db, new FakeTenantLifetimes(),
            Options.Create(new ProtocolTokenOptions()), Options.Create(new ProtocolSessionOptions()),
            new FakeRefreshTokenRetention(null));

        var result = await handler.Handle(Command(refreshTokenAbsolute: TimeSpan.FromDays(180)), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
