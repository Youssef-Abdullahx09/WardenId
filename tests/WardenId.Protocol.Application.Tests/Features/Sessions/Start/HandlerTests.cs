using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Features.Sessions.Start;
using WardenId.Protocol.Domain.TokenPolicies;
using Command = WardenId.Protocol.Application.Features.Sessions.Start.Command;

namespace WardenId.Protocol.Application.Tests.Features.Sessions.Start;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Handler CreateHandler(
        TestProtocolDbContext db, TimeProvider clock, TimeSpan absoluteLifetime, FakeAuthAuditTrail? audit = null,
        TenantLifetimeOverrides? tenant = null) =>
        new(db, clock,
            new FakeTenantLifetimes(sessions: new ProtocolSessionOptions { AbsoluteLifetime = absoluteLifetime }, overrides: tenant),
            audit ?? new FakeAuthAuditTrail());

    [Fact]
    public async Task Start_UsesTheTenantAbsoluteLifetime_OverGlobal()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());
        var tenant = TenantLifetimeOverrides.Create(null, null, null, null, null, null, TimeSpan.FromHours(2)).Value;
        var handler = CreateHandler(db, new FakeTimeProvider(SessionBuilder.Now), TimeSpan.FromHours(12), tenant: tenant);

        var result = await handler.Handle(new Command(TenantId, UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionBuilder.Now.AddHours(2), (await db.Sessions.SingleAsync()).ExpiresAt);
    }

    [Fact]
    public async Task Start_PersistsLiveSession_WithAbsoluteExpiry()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var db = TestProtocolDbContext.CreateNew(databaseName);
        var handler = CreateHandler(db, new FakeTimeProvider(SessionBuilder.Now), TimeSpan.FromHours(12));

        var result = await handler.Handle(new Command(TenantId, UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionBuilder.Now, result.Value.AuthTime);

        await using var check = TestProtocolDbContext.CreateNew(databaseName);
        var session = await check.Sessions.SingleAsync();
        Assert.Equal(result.Value.Sid, session.Id);
        Assert.Equal(TenantId, session.TenantId);
        Assert.Equal(UserId, session.SubjectId);
        Assert.Equal(SessionBuilder.Now, session.AuthTime);
        Assert.Equal(SessionBuilder.Now.AddHours(12), session.ExpiresAt);
        Assert.Null(session.EndedAt);
    }

    [Fact]
    public async Task Start_TruncatesAuthTimeToWholeSeconds()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());
        var handler = CreateHandler(db, new FakeTimeProvider(SessionBuilder.Now.AddMilliseconds(750)), TimeSpan.FromDays(1));

        var result = await handler.Handle(new Command(TenantId, UserId), CancellationToken.None);

        Assert.Equal(SessionBuilder.Now, result.Value.AuthTime);
    }

    [Fact]
    public async Task Start_NonPositiveLifetime_FailsValidation_AndPersistsNothing()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());
        var handler = CreateHandler(db, new FakeTimeProvider(SessionBuilder.Now), TimeSpan.Zero);

        var result = await handler.Handle(new Command(TenantId, UserId), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.Validation, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
        Assert.Empty(db.Sessions);
    }

    [Fact]
    public async Task Start_RecordsTheInteractiveSignIn_WithSidAuthTimeAndPasswordAmr()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());
        var audit = new FakeAuthAuditTrail();
        var handler = CreateHandler(db, new FakeTimeProvider(SessionBuilder.Now), TimeSpan.FromHours(12), audit);

        var result = await handler.Handle(new Command(TenantId, UserId), CancellationToken.None);

        var recorded = Assert.Single(audit.Recorded);
        Assert.Equal(SecurityEvent.SessionStarted, recorded.Event);
        Assert.Equal(TenantId, recorded.TenantId);
        Assert.Equal(SessionBuilder.Now, recorded.OccurredAt);
        Assert.Equal(Actor.User(UserId), recorded.Actor);
        Assert.Equal([Target.User(UserId), Target.Session(result.Value.Sid)], recorded.Targets);
        Assert.Null(recorded.Reason);
        Assert.Equal(result.Value.Sid, recorded.Session!.Sid);
        Assert.Equal(SessionBuilder.Now, recorded.Session.AuthTime);
        Assert.Equal(["pwd"], recorded.Session.Amr);
    }

    [Fact]
    public async Task Start_Failure_RecordsNothing()
    {
        await using var db = TestProtocolDbContext.CreateNew(Guid.NewGuid().ToString());
        var audit = new FakeAuthAuditTrail();
        var handler = CreateHandler(db, new FakeTimeProvider(SessionBuilder.Now), TimeSpan.Zero, audit);

        await handler.Handle(new Command(TenantId, UserId), CancellationToken.None);

        Assert.Empty(audit.Recorded);
    }
}
