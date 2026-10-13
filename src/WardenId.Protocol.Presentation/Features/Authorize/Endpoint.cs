using System.Globalization;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using WardenId.SharedKernel.Application;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.Presentation;
using ValidateSessionUserQuery = WardenId.Protocol.Application.Features.Authorize.ValidateSessionUser.Query;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Presentation.Features.Authorize;

/// <summary>
/// OpenIddict passthrough target: by the time this runs OpenIddict has validated the tenant, the
/// client, the exact redirect_uri, response_type, scopes and PKCE. This decides offline_access
/// eligibility and who the user is (tenant session cookie), and signs in so OpenIddict issues the code.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapMethods(ProtocolRoutes.TenantIssuer + "/authorize", [HttpMethods.Get, HttpMethods.Post], async (
                HttpContext httpContext,
                [FromServices] ITenantContext tenantContext,
                [FromServices] IAntiforgery antiforgery,
                [FromServices] ISender sender,
                [FromServices] IOpenIddictApplicationManager applicationManager,
                CancellationToken cancellationToken) =>
            {
                // Null when OpenIddict didn't handle the request (e.g. mixed-case slug).
                var request = httpContext.GetOpenIddictServerRequest();
                if (request is null
                    || tenantContext.TenantId is not { } tenantId
                    || tenantContext.Slug is not { } slug)
                {
                    return Results.NotFound();
                }

                if (HttpMethods.IsPost(httpContext.Request.Method)
                    && request.GetParameter(AuthorizeRequestExtensions.CancelParameter) is not null)
                {
                    // Only our own pages post cancel; RPs posting authorize requests never send it.
                    if (!await antiforgery.IsRequestValidAsync(httpContext)) return Results.BadRequest();
                    return OpenIddictResults.Forbid(Errors.AccessDenied, "The user cancelled the sign-in.");
                }

                if (!request.HasScope(Scopes.OpenId))
                    return OpenIddictResults.Forbid(Errors.InvalidScope, "The 'openid' scope is required.");

                // Duende-style: offline_access from a client without the RefreshToken grant is a redirected
                // invalid_scope, before any login. (OpenIddict's own check is removed — it answers locally.)
                if (request.HasScope(Scopes.OfflineAccess)
                    && (await applicationManager.FindByClientIdAsync(request.ClientId!, cancellationToken) is not { } application
                        || !await applicationManager.HasPermissionAsync(application, Permissions.GrantTypes.RefreshToken, cancellationToken)))
                {
                    return OpenIddictResults.Forbid(Errors.InvalidScope, "The client is not allowed to request offline access.");
                }

                var session = await TenantSession.ReadAsync(httpContext, tenantId);

                // The user may have been disabled/locked or given a required action since signing in.
                if (session is not null
                    && (await sender.Send(new ValidateSessionUserQuery(tenantId, session.UserId), cancellationToken)).IsFailed)
                {
                    await httpContext.SignOutAsync(TenantSession.Scheme);
                    session = null;
                }

                if (session is null)
                {
                    if (request.HasPromptValue(PromptValues.None))
                        return OpenIddictResults.Forbid(Errors.LoginRequired, "The user is not signed in.");

                    return Results.Redirect(
                        $"/tenants/{slug}/login?ReturnUrl={Uri.EscapeDataString(request.ToReturnUrl(slug))}");
                }

                var identity = new ClaimsIdentity(
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
                identity.SetClaim(Claims.Subject, session.UserId.ToString());
                identity.SetClaim(CustomClaimTypes.TenantId, tenantId.ToString());
                identity.AddClaim(new Claim(
                    Claims.AuthenticationTime,
                    session.AuthTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64));

                // Destinations are decided at redemption (Token endpoint), where claims are rebuilt.
                var principal = new ClaimsPrincipal(identity);
                principal.SetScopes(request.GetScopes());

                return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            })
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithTags("Protocol - Authorize")
            .WithSummary("OAuth 2.0 / OIDC authorization endpoint (code + PKCE)")
            .ExcludeFromDescription();
}
