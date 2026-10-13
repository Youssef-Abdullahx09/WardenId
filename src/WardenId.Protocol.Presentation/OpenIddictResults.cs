using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using OpenIddict.Server.AspNetCore;

namespace WardenId.Protocol.Presentation;

internal static class OpenIddictResults
{
    /// <summary>
    /// Hands an OAuth error back to OpenIddict, which renders it per endpoint (JSON body for /token,
    /// redirect to the client for /authorize).
    /// </summary>
    public static IResult Forbid(string error, string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

    /// <summary>
    /// A bearer-token error for OpenIddict to render (WWW-Authenticate header): invalid_token → 401,
    /// insufficient_scope → 403. Used by the userinfo passthrough.
    /// </summary>
    public static IResult Challenge(string error, string description) =>
        Results.Challenge(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
