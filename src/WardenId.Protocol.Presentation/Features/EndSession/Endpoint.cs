using MediatR;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using WardenId.Protocol.Application;
using WardenId.Protocol.Domain.Sessions;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.Presentation;
using static OpenIddict.Abstractions.OpenIddictConstants;
using EndSessionCommand = WardenId.Protocol.Application.Features.Sessions.End.Command;

namespace WardenId.Protocol.Presentation.Features.EndSession;

/// <summary>
/// OpenIddict passthrough target for OIDC RP-Initiated Logout. By the time this runs, OpenIddict has
/// validated id_token_hint (tenant keys and issuer; expiry tolerated; an invalid hint is dropped),
/// client_id, the exact post_logout_redirect_uri and the hint's aud/presenter. This decides whether
/// the user must confirm (D7), ends the session row and clears the cookie. OpenIddict then redirects
/// to the validated URI with state, or to the signed-out page.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    private const string ConfirmParameter = "confirm";
    private const string CancelParameter = "cancel";

    /// <summary>The RP-Initiated Logout parameters carried to the confirmation page; everything else is dropped.</summary>
    internal static readonly IReadOnlyList<string> ForwardedParameters =
    [
        Parameters.IdTokenHint, Parameters.ClientId, Parameters.PostLogoutRedirectUri,
        Parameters.State, Parameters.UiLocales, "logout_hint"
    ];

    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapMethods(ProtocolRoutes.TenantIssuer + "/endsession", [HttpMethods.Get, HttpMethods.Post], async (
                HttpContext httpContext,
                [FromServices] ITenantContext tenantContext,
                [FromServices] IAntiforgery antiforgery,
                [FromServices] ISender sender,
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

                // Only our confirmation page posts confirm/cancel. A link or cross-site form can't carry the
                // antiforgery token, so it can neither force a logout nor bounce the user to the RP.
                var isPost = HttpMethods.IsPost(httpContext.Request.Method);
                var confirmed = isPost && request.GetParameter(ConfirmParameter) is not null;
                var cancelled = isPost && request.GetParameter(CancelParameter) is not null;
                if ((confirmed || cancelled) && !await antiforgery.IsRequestValidAsync(httpContext))
                    return Results.BadRequest();

                if (cancelled)
                    return Results.Redirect(CancelLocation(request, slug));

                var session = await TenantSession.ReadAsync(httpContext, tenantId, sender, cancellationToken);
                if (session is not null)
                {
                    if (!confirmed && !await HintNamesSessionAsync(httpContext, session.Sid))
                        return Results.Redirect(ConfirmationLocation(request, slug));

                    // RequireExisting: false. A session that ended concurrently (password change, admin) is
                    // already logged out, so End succeeds either way.
                    await sender.Send(
                        new EndSessionCommand(tenantId, session.Sid, EndReason.UserLogout, RequireExisting: false),
                        cancellationToken);
                    await httpContext.SignOutAsync(TenantSession.Scheme);
                }

                // A6: OpenIddict redirects to the validated post_logout_redirect_uri (+ state), or to RedirectUri.
                return Results.SignOut(
                    new AuthenticationProperties { RedirectUri = $"/tenants/{slug}/signed-out" },
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            })
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithTags("Protocol - EndSession")
            .WithSummary("OIDC RP-Initiated Logout (end session) endpoint")
            .ExcludeFromDescription();

    /// <summary>
    /// True when id_token_hint was issued for this browser's session. OpenIddict's principal is empty
    /// when there was no hint or it was invalid (A3), and then the user must confirm.
    /// </summary>
    private static async Task<bool> HintNamesSessionAsync(HttpContext httpContext, Guid sid)
    {
        var hint = await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        return Guid.TryParse(hint.Principal?.GetClaim(SessionClaimTypes.SessionId), out var hintSid) && hintSid == sid;
    }

    private static string ConfirmationLocation(OpenIddictRequest request, string slug) =>
        QueryHelpers.AddQueryString($"/tenants/{slug}/logout", request.GetParameters()
            .Where(p => ForwardedParameters.Contains(p.Key))
            .Select(p => KeyValuePair.Create(p.Key, (string?)p.Value.ToString())));

    // A7: request.PostLogoutRedirectUri is present here only if OpenIddict validated it for the client.
    // An unregistered URI was rejected (400) before passthrough, so this never redirects anywhere else.
    private static string CancelLocation(OpenIddictRequest request, string slug)
    {
        if (string.IsNullOrEmpty(request.PostLogoutRedirectUri))
            return $"/tenants/{slug}/logout?cancelled=true";

        return string.IsNullOrEmpty(request.State)
            ? request.PostLogoutRedirectUri
            : QueryHelpers.AddQueryString(request.PostLogoutRedirectUri, Parameters.State, request.State);
    }
}
