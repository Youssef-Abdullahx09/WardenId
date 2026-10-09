using System.Security.Claims;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using WardenId.Protocol.Application;
using WardenId.Protocol.Application.Features.Token.ClientCredentials;
using WardenId.SharedKernel.Application;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Presentation.Features.Token;

/// <summary>Translates the grant decided by the handler into the principal OpenIddict signs in.</summary>
public static class ClientCredentialsGrantExtensions
{
    public static ClaimsPrincipal ToPrincipal(this ClientCredentialsGrant grant)
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, grant.Subject);
        identity.SetClaim(CustomClaimTypes.TenantId, grant.TenantId.ToString());
        identity.SetClaim(CustomClaimTypes.TokenUse, TokenUse.Access);
        identity.SetDestinations(static _ => [Destinations.AccessToken]);

        var principal = new ClaimsPrincipal(identity);
        principal.SetResources(grant.Audience);
        principal.SetAccessTokenLifetime(grant.AccessTokenLifetime);
        return principal;
    }
}
