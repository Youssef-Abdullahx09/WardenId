namespace WardenId.Protocol.Application.Features.BackchannelLogout;

/// <summary>POSTs a logout_token to an RP and classifies the answer (spec D10).</summary>
public interface IBackchannelLogoutSender
{
    Task<DeliveryOutcome> SendAsync(Uri backchannelLogoutUri, string logoutToken, CancellationToken cancellationToken);
}
