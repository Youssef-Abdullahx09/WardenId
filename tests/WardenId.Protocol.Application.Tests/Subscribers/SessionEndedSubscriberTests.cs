using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Domain.Sessions;
using SessionEnded = WardenId.Protocol.Application.Subscribers.SessionEndedDomainEvent;

namespace WardenId.Protocol.Application.Tests.Subscribers;

public class SessionEndedSubscriberTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid Sid = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    private static SessionEnded.Message Ended(params SessionAuthorizationSnapshot[] authorizations) =>
        new(Sid, TenantId, SubjectId, EndReason.UserLogout, authorizations);

    private static (SessionEnded.Subscriber Subscriber, FakeSessionAuthorizationRevoker Revoker, FakeBackchannelLogoutQueue Queue) Create()
    {
        var revoker = new FakeSessionAuthorizationRevoker();
        var queue = new FakeBackchannelLogoutQueue();
        return (new SessionEnded.Subscriber(revoker, queue), revoker, queue);
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
    public async Task OnReceive_NoAuthorizations_DoesNothing()
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
}
