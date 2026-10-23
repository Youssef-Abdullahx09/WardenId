using System.Security.Claims;
using System.Xml;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using WardenId.Protocol.Application.Features.Token;
using WardenId.Protocol.Application.Utilities;
using WardenId.SharedKernel.Application.Utilities;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Presentation.Features.Token;

/// <summary>
/// Rebuilds the principal from the redeemed code or refresh token (keeping OpenIddict's private claims:
/// scopes, presenters, nonce, authorization id, and auth_time for the absolute cap) and applies fresh
/// user claims, destinations and lifetimes.
/// </summary>
public static class UserTokenGrantExtensions
{
    public static ClaimsPrincipal ToPrincipal(this UserTokenGrant grant, ClaimsPrincipal sourcePrincipal)
    {
        var identity = new ClaimsIdentity(
            sourcePrincipal.Claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, grant.Subject);
        identity.SetClaim(CustomClaimTypes.TenantId, grant.TenantId.ToString());
        identity.SetClaim(CustomClaimTypes.TokenUse, TokenUse.Access);
        identity.SetClaim(Claims.PreferredUsername, grant.PreferredUsername);
        identity.SetClaim(Claims.Email, grant.Email);
        identity.RemoveClaims(Claims.EmailVerified);
        if (grant.EmailVerified is { } verified)
            identity.AddClaim(new Claim(Claims.EmailVerified, XmlConvert.ToString(verified), ClaimValueTypes.Boolean));

        identity.SetDestinations(static claim => claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
            CustomClaimTypes.TenantId or CustomClaimTypes.TokenUse => [Destinations.AccessToken],
            // sid (OIDC Back-Channel Logout §2.1) is copied from the code/refresh principal like auth_time.
            Claims.AuthenticationTime or Claims.PreferredUsername or Claims.Email or Claims.EmailVerified
                or SessionClaimTypes.SessionId
                => [Destinations.IdentityToken],
            _ => []
        });

        var principal = new ClaimsPrincipal(identity);
        principal.SetResources(grant.Audience);
        principal.SetAccessTokenLifetime(grant.AccessTokenLifetime);
        principal.SetIdentityTokenLifetime(grant.IdTokenLifetime);
        // OpenIddict issues a refresh token whenever offline_access is among the principal's scopes (falling
        // back to its global lifetime), so a grant without a refresh lifetime (e.g. the client lost the refresh
        // grant after /authorize) must drop offline_access to suppress it.
        if (grant.RefreshTokenLifetime is { } refreshLifetime)
            principal.SetRefreshTokenLifetime(refreshLifetime);
        else if (principal.HasScope(Scopes.OfflineAccess))
            principal.SetScopes(principal.GetScopes().Remove(Scopes.OfflineAccess));
        return principal;
    }
}
