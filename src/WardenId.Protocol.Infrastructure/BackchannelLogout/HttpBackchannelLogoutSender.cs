using System.Net;
using Microsoft.Extensions.Logging;
using WardenId.Protocol.Application;
using WardenId.Protocol.Application.Features.BackchannelLogout;

namespace WardenId.Protocol.Infrastructure.BackchannelLogout;

/// <summary>
/// POSTs the logout_token form (Back-Channel Logout §2.5) and classifies the answer (spec D10).
/// The typed client's primary handler never follows redirects (AddProtocolBackchannelLogout), so a 3xx is
/// the RP's final answer: Rejected. A redirect is never followed to a URI the client didn't register.
/// </summary>
public sealed class HttpBackchannelLogoutSender(HttpClient httpClient, ILogger<HttpBackchannelLogoutSender> logger)
    : IBackchannelLogoutSender
{
    public async Task<DeliveryOutcome> SendAsync(Uri backchannelLogoutUri, string logoutToken, CancellationToken cancellationToken)
    {
        // Same rule as ClientManagement's BackchannelLogoutUri, re-applied in case the stored value predates it (spec D13).
        if (!IsAllowedTarget(backchannelLogoutUri))
        {
            logger.LogWarning("Back-channel logout URI {Uri} is not https or loopback http; not sent.", backchannelLogoutUri);
            return DeliveryOutcome.Rejected;
        }

        using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>(OidcValues.Parameters.LogoutToken, logoutToken)]);
        try
        {
            using var response = await httpClient.PostAsync(backchannelLogoutUri, content, cancellationToken);
            return Classify(response.StatusCode);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Back-channel logout POST to {Uri} failed.", backchannelLogoutUri);
            return DeliveryOutcome.TransientFailure;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout surfaces as TaskCanceledException. Caller cancellation (shutdown) still propagates.
            logger.LogWarning(exception, "Back-channel logout POST to {Uri} timed out.", backchannelLogoutUri);
            return DeliveryOutcome.TransientFailure;
        }
    }

    private static bool IsAllowedTarget(Uri uri) =>
        uri.IsAbsoluteUri
        && string.IsNullOrEmpty(uri.Fragment)
        && string.IsNullOrEmpty(uri.UserInfo)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));

    // 1xx/3xx/4xx: the RP refused (or redirected). Retrying cannot help.
    private static DeliveryOutcome Classify(HttpStatusCode status) => (int)status switch
    {
        >= 200 and < 300 => DeliveryOutcome.Delivered,
        >= 500 => DeliveryOutcome.TransientFailure,
        _ => DeliveryOutcome.Rejected
    };
}
