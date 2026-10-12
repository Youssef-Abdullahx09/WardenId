using System.Security.Claims;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using WardenId.Protocol.Application;
using WardenId.Protocol.Application.Features.Token.AuthorizationCode;
using WardenId.SharedKernel.Application;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Presentation.Features.Token;

/// <summary>
/// Rebuilds the principal from the redeemed code (keeping OpenIddict's private claims: scopes,
/// presenters, nonce, authorization id) and applies fresh user claims and destinations.
/// </summary>
public static class AuthorizationCodeGrantExtensions
{
    public static ClaimsPrincipal ToPrincipal(this AuthorizationCodeGrant grant, ClaimsPrincipal codePrincipal)
    {
        var identity = new ClaimsIdentity(
            codePrincipal.Claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, grant.Subject);
        identity.SetClaim(CustomClaimTypes.TenantId, grant.TenantId.ToString());
        identity.SetClaim(CustomClaimTypes.TokenUse, TokenUse.Access);
        identity.SetClaim(Claims.PreferredUsername, grant.PreferredUsername);
        identity.SetClaim(Claims.Email, grant.Email);
        identity.RemoveClaims(Claims.EmailVerified);
        if (grant.EmailVerified is { } verified)
            identity.AddClaim(new Claim(Claims.EmailVerified, verified ? "true" : "false", ClaimValueTypes.Boolean));

        identity.SetDestinations(static claim => claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
            CustomClaimTypes.TenantId or CustomClaimTypes.TokenUse => [Destinations.AccessToken],
            Claims.AuthenticationTime or Claims.PreferredUsername or Claims.Email or Claims.EmailVerified
                => [Destinations.IdentityToken],
            _ => []
        });

        var principal = new ClaimsPrincipal(identity);
        principal.SetResources(grant.Audience);
        principal.SetAccessTokenLifetime(grant.AccessTokenLifetime);
        principal.SetIdentityTokenLifetime(grant.IdTokenLifetime);
        return principal;
    }
}
