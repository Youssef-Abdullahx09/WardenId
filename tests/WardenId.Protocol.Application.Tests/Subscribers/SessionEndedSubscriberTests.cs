using Microsoft.Extensions.Time.Testing;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Domain.Sessions;
using SessionEnded = WardenId.Protocol.Application.Subscribers.SessionEndedDomainEvent;

namespace WardenId.Protocol.Application.Tests.Subscribers;

public class SessionEndedSubscriberTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid Sid = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();
    private static readonly DateTimeOffset EndedAt = SessionBuilder.Now.AddMinutes(-1);

    private static SessionEnded.Message Ended(params SessionAuthorizationSnapshot[] authorizations) =>
        new(Sid, TenantId, SubjectId, EndReason.UserLogout, authorizations, EndedAt);

    private static (SessionEnded.Subscriber Subscriber, FakeSessionAuthorizationRevoker Revoker, FakeBackchannelLogoutQueue Queue) Create() =>
        Create(new FakeAuthAuditTrail());

    private static (SessionEnded.Subscriber Subscriber, FakeSessionAuthorizationRevoker Revoker, FakeBackchannelLogoutQueue Queue) Create(
        FakeAuthAuditTrail audit)
    {
        var revoker = new FakeSessionAuthorizationRevoker();
        var queue = new FakeBackchannelLogoutQueue();
        return (new SessionEnded.Subscriber(revoker, queue, audit, new FakeTimeProvider(SessionBuilder.Now)), revoker, queue);
    }

    [Fact]
    public async Task OnReceive_RevokesOnlyNonOfflineAuthorizations()
    {
        var (subscriber, revoker, _) = Create();

        await subscriber.OnReceive(
            Ended(new SessionAuthorizationSnapshot("a1", "web", false), new SessionAuthorizationSnapshot("a2", "web", true), new SessionAuthorizationSnapshot("a3", "spa", false)));

        Assert.Equal(new[] { "a1", "a3" }, Assert.Single(revoker.Calls));
    }

    [Fact]
    public async Task OnReceive_EnqueuesOneMessagePerDistinctClient_IncludingOfflineOnlyClients()
    {
        var (subscriber, _, queue) = Create();

        await subscriber.OnReceive(
            Ended(new SessionAuthorizationSnapshot("a1", "web", false), new SessionAuthorizationSnapshot("a2", "web", true), new SessionAuthorizationSnapshot("a3", "spa", true)));

        Assert.Equal(
            new[]
            {
                new BackchannelLogoutRequested(TenantId, "web", Sid, SubjectId),
                new BackchannelLogoutRequested(TenantId, "spa", Sid, SubjectId)
            },
            queue.Enqueued);
    }

    [Fact]
    public async Task OnReceive_ComparesClientIdsOrdinally()
    {
        var (subscriber, _, queue) = Create();

        await subscriber.OnReceive(Ended(new SessionAuthorizationSnapshot("a1", "web", false), new SessionAuthorizationSnapshot("a2", "WEB", false)));

        Assert.Equal(new[] { "web", "WEB" }, queue.Enqueued.Select(message => message.ClientId));
    }

    [Fact]
    public async Task OnReceive_RevokesBeforeEnqueueing()
    {
        var (subscriber, revoker, queue) = Create();
        var revocationsSeenAtFirstEnqueue = -1;
        queue.OnEnqueue = _ =>
        {
            if (revocationsSeenAtFirstEnqueue < 0) revocationsSeenAtFirstEnqueue = revoker.Calls.Count;
        };

        await subscriber.OnReceive(Ended(new SessionAuthorizationSnapshot("a1", "web", false)));

        Assert.Equal(1, revocationsSeenAtFirstEnqueue);
    }

    [Fact]
    public async Task OnReceive_OnlyOfflineAuthorizations_SkipsRevoker_ButStillNotifies()
    {
        var (subscriber, revoker, queue) = Create();

        await subscriber.OnReceive(Ended(new SessionAuthorizationSnapshot("a1", "web", true)));

        Assert.Empty(revoker.Calls);
        Assert.Single(queue.Enqueued);
    }

    [Fact]
    public async Task OnReceive_NoAuthorizations_NeitherRevokesNorNotifies()
    {
        var (subscriber, revoker, queue) = Create();

        await subscriber.OnReceive(Ended());

        Assert.Empty(revoker.Calls);
        Assert.Empty(queue.Enqueued);
    }

    [Fact]
    public async Task OnReceive_RevocationFailure_Throws_SoCapRetries_AndNothingIsEnqueued()
    {
        var (subscriber, revoker, queue) = Create();
        revoker.Throw = new InvalidOperationException("db down");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subscriber.OnReceive(Ended(new SessionAuthorizationSnapshot("a1", "web", false))));

        Assert.Empty(queue.Enqueued);
    }

    [Theory]
    [InlineData(EndReason.UserLogout, OutcomeReason.UserLogout)]
    [InlineData(EndReason.PasswordChanged, OutcomeReason.PasswordChanged)]
    [InlineData(EndReason.UserDisabled, OutcomeReason.UserDisabled)]
    [InlineData(EndReason.UserDeleted, OutcomeReason.UserDeleted)]
    [InlineData(EndReason.AdminEnded, OutcomeReason.AdminEnded)]
    [InlineData(EndReason.AccountLocked, OutcomeReason.AccountLocked)]
    public async Task OnReceive_RecordsTheSessionEnd_WithItsReason(EndReason reason, OutcomeReason expected)
    {
        var audit = new FakeAuthAuditTrail();
        var (subscriber, _, _) = Create(audit);

        await subscriber.OnReceive(new SessionEnded.Message(Sid, TenantId, SubjectId, reason, [], EndedAt));

        var recorded = Assert.Single(audit.Recorded);
        Assert.Equal(SecurityEvent.SessionEnded, recorded.Event);
        Assert.Equal(TenantId, recorded.TenantId);
        Assert.Equal(EndedAt, recorded.OccurredAt);
        Assert.Equal(Actor.System("protocol"), recorded.Actor);
        Assert.Equal([Target.User(SubjectId), Target.Session(Sid)], recorded.Targets);
        Assert.Equal(expected, recorded.Reason);
        Assert.Equal(Sid, recorded.Session!.Sid);
    }

    [Fact]
    public async Task OnReceive_MessageWithoutEndTime_RecordsTheReceiveTime()
    {
        var audit = new FakeAuthAuditTrail();
        var (subscriber, _, _) = Create(audit);

        await subscriber.OnReceive(new SessionEnded.Message(Sid, TenantId, SubjectId, EndReason.UserLogout, [], default));

        Assert.Equal(SessionBuilder.Now, Assert.Single(audit.Recorded).OccurredAt);
    }

    [Fact]
    public async Task OnReceive_RecordsAfterTheFanOut_SoARetriedMessageIsAuditedOnce()
    {
        var audit = new FakeAuthAuditTrail();
        var (subscriber, revoker, _) = Create(audit);
        revoker.Throw = new InvalidOperationException("db down");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subscriber.OnReceive(Ended(new SessionAuthorizationSnapshot("a1", "web", false))));

        Assert.Empty(audit.Recorded);
    }
}
