using WardenId.Protocol.Application.Features.BackchannelLogout;

namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeBackchannelLogoutSender : IBackchannelLogoutSender
{
    public sealed record Call(Uri Uri, string LogoutToken);

    public DeliveryOutcome Outcome { get; set; } = DeliveryOutcome.Delivered;

    public List<Call> Calls { get; } = [];

    public Task<DeliveryOutcome> SendAsync(Uri backchannelLogoutUri, string logoutToken, CancellationToken cancellationToken)
    {
        Calls.Add(new Call(backchannelLogoutUri, logoutToken));
        return Task.FromResult(Outcome);
    }
}
