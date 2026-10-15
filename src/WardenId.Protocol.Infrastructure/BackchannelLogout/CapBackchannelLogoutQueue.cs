using DotNetCore.CAP;
using WardenId.Protocol.Application.Features.BackchannelLogout;

namespace WardenId.Protocol.Infrastructure.BackchannelLogout;

/// <summary>
/// The seam in front of ICapPublisher (like Audit's CapAuditSegmentPublisher), so Application tests never fake CAP.
/// It is called from SessionEndedSubscriber, which runs with no EF transaction and no SaveChanges. CAP then
/// stores the message in its Postgres outbox and dispatches it directly.
/// </summary>
public sealed class CapBackchannelLogoutQueue(ICapPublisher capPublisher) : IBackchannelLogoutQueue
{
    public Task EnqueueAsync(BackchannelLogoutRequested message, CancellationToken cancellationToken) =>
        capPublisher.PublishAsync(BackchannelLogoutRequested.Topic, message, cancellationToken: cancellationToken);
}
