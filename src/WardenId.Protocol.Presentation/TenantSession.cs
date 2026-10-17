using System.Globalization;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WardenId.Protocol.Application;
using WardenId.SharedKernel.Application;
using static OpenIddict.Abstractions.OpenIddictConstants;
using GetLiveSessionQuery = WardenId.Protocol.Application.Features.Sessions.GetLive.Query;

namespace WardenId.Protocol.Presentation;

public sealed record TenantSessionInfo(Guid UserId, DateTimeOffset AuthTime, Guid Sid);

/// <summary>
/// The per-tenant login session shared by the login page, /authorize and /endsession. The cookie is only
/// a reference: its <c>sid</c> must name a live Session row (Protocol.Application) — the database is the
/// source of truth, so ending the row ends the browser session.
/// </summary>
public static class TenantSession
{
    public const string Scheme = "WardenId.TenantSession";

    /// <summary>
    /// Issues the cookie with the tenant's idle lifetime (<paramref name="cookieLifetime"/>): the ticket carries its
    /// own issued/expires pair, and sliding renewal keeps that span, so tenants can differ under one scheme.
    /// </summary>
    public static Task SignInAsync(
        HttpContext httpContext, Guid tenantId, Guid userId, Guid sid, DateTimeOffset authTime, TimeSpan cookieLifetime)
    {
        var identity = new ClaimsIdentity(Scheme, Claims.Subject, Claims.Role);
        identity.AddClaim(new Claim(Claims.Subject, userId.ToString()));
        identity.AddClaim(new Claim(CustomClaimTypes.TenantId, tenantId.ToString()));
        identity.AddClaim(new Claim(SessionClaimTypes.SessionId, sid.ToString()));
        identity.AddClaim(new Claim(
            Claims.AuthenticationTime,
            authTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64));
        identity.AddClaim(new Claim(Claims.AuthenticationMethodReference, "pwd"));

        var now = httpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var properties = new AuthenticationProperties { IssuedUtc = now, ExpiresUtc = now + cookieLifetime };
        return httpContext.SignInAsync(Scheme, new ClaimsPrincipal(identity), properties);
    }

    /// <summary>
    /// The live session for <paramref name="tenantId"/>, or null. A ticket that fails any check — another
    /// tenant, a pre-session cookie without <c>sid</c>, an ended/expired/foreign row — is signed out so the
    /// browser drops it.
    /// </summary>
    public static async Task<TenantSessionInfo?> ReadAsync(
        HttpContext httpContext, Guid tenantId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await httpContext.AuthenticateAsync(Scheme);
        if (!result.Succeeded) return null;

        var session = await ValidateAsync(result.Principal, tenantId, sender, cancellationToken);
        if (session is null) await httpContext.SignOutAsync(Scheme);
        return session;
    }

    private static async Task<TenantSessionInfo?> ValidateAsync(
        ClaimsPrincipal principal, Guid tenantId, ISender sender, CancellationToken cancellationToken)
    {
        if (principal.FindFirst(CustomClaimTypes.TenantId)?.Value != tenantId.ToString()) return null;
        if (!Guid.TryParse(principal.FindFirst(Claims.Subject)?.Value, out var userId)) return null;
        if (!Guid.TryParse(principal.FindFirst(SessionClaimTypes.SessionId)?.Value, out var sid)) return null;

        var live = await sender.Send(new GetLiveSessionQuery(tenantId, sid), cancellationToken);
        if (live.IsFailed || live.Value.UserId != userId) return null;

        return new TenantSessionInfo(userId, live.Value.AuthTime, sid);
    }
}
