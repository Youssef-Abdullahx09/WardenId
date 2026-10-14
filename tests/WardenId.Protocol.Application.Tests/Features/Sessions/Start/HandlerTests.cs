using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WardenId.Protocol.Application.Features.Sessions.Start;

namespace WardenId.Protocol.Application.Tests.Features.Sessions.Start;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Handler CreateHandler(TestProtocolDbContext db, TimeProvider clock, TimeSpan absoluteLifetime) =>
        new(db, clock, Options.Create(new ProtocolSessionOptions { AbsoluteLifetime = absoluteLifetime }));

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
}
