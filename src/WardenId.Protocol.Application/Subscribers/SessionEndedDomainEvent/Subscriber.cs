using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.SharedKernel.Application.CapPublisher;
using Requested = WardenId.Protocol.Application.Features.BackchannelLogout.BackchannelLogoutRequested;

namespace WardenId.Protocol.Application.Subscribers.SessionEndedDomainEvent;

/// <summary>
/// Protocol consuming its own SessionEndedDomainEvent, the first hop of the back-channel fan-out (spec D5/D8).
/// <list type="number">
/// <item>Revokes the session's grants that lack offline_access. Offline grants are meant to outlive the browser session.</item>
/// <item>Queues one delivery per participating client. Clients holding only offline grants are still told the session ended.</item>
/// </list>
/// Revocation runs first: if it throws, CAP retries the whole message before any RP is told.
/// </summary>
public sealed class Subscriber(ISessionAuthorizationRevoker revoker, IBackchannelLogoutQueue queue) : ICapSubscriber<Message>
{
    // Group is explicit: CAP silently drops one of two subscriptions sharing (topic, group).
    [CapSubscriber(nameof(SessionEndedDomainEvent), Group = "protocol." + nameof(SessionEndedDomainEvent))]
    public async Task OnReceive(Message message)
    {
        var revocable = message.Authorizations
            .Where(authorization => !authorization.OfflineAccess)
            .Select(authorization => authorization.AuthorizationId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (revocable.Count > 0)
            await revoker.RevokeAsync(revocable, CancellationToken.None);

        var clientIds = message.Authorizations
            .Select(authorization => authorization.ClientId)
            .Distinct(StringComparer.Ordinal);
        foreach (var clientId in clientIds)
        {
            await queue.EnqueueAsync(
                new Requested(message.TenantId, clientId, message.Sid, message.SubjectId),
                CancellationToken.None);
        }
    }
}
