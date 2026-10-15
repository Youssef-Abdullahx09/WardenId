namespace WardenId.Protocol.Application.Features.BackchannelLogout;

/// <summary>Durably queues one delivery (CAP in production) so the user-facing logout never waits on RPs.</summary>
public interface IBackchannelLogoutQueue
{
    Task EnqueueAsync(BackchannelLogoutRequested message, CancellationToken cancellationToken);
}
