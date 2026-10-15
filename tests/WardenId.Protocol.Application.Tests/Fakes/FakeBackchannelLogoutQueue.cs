using WardenId.Protocol.Application.Features.BackchannelLogout;

namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeBackchannelLogoutQueue : IBackchannelLogoutQueue
{
    public List<BackchannelLogoutRequested> Enqueued { get; } = [];

    /// <summary>Runs before each message is recorded, so a test can observe ordering against other fakes.</summary>
    public Action<BackchannelLogoutRequested>? OnEnqueue { get; set; }

    public Task EnqueueAsync(BackchannelLogoutRequested message, CancellationToken cancellationToken)
    {
        OnEnqueue?.Invoke(message);
        Enqueued.Add(message);
        return Task.CompletedTask;
    }
}
