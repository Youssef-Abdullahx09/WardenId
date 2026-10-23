using System.Globalization;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WardenId.Protocol.Application.Features.Policies;
using WardenId.Protocol.Application.Utilities;
using WardenId.Protocol.Domain.Sessions;
using WardenId.SharedKernel.Application.Utilities;
using static OpenIddict.Abstractions.OpenIddictConstants;
using EndSessionCommand = WardenId.Protocol.Application.Features.Sessions.End.Command;
using GetLiveSessionQuery = WardenId.Protocol.Application.Features.Sessions.GetLive.Query;
using StartSessionCommand = WardenId.Protocol.Application.Features.Sessions.Start.Command;

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
        HttpContext httpContext, Guid tenantId, Guid userId, Guid sid, DateTimeOffset authTime, TimeSpan cookieLifetime,
        IReadOnlyList<string> amr)
    {
        var identity = new ClaimsIdentity(Scheme, Claims.Subject, Claims.Role);
        identity.AddClaim(new Claim(Claims.Subject, userId.ToString()));
        identity.AddClaim(new Claim(CustomClaimTypes.TenantId, tenantId.ToString()));
        identity.AddClaim(new Claim(SessionClaimTypes.SessionId, sid.ToString()));
        identity.AddClaim(new Claim(
            Claims.AuthenticationTime,
            authTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64));
        foreach (var method in amr) identity.AddClaim(new Claim(Claims.AuthenticationMethodReference, method));

        var now = httpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var properties = new AuthenticationProperties { IssuedUtc = now, ExpiresUtc = now + cookieLifetime };
        return httpContext.SignInAsync(Scheme, new ClaimsPrincipal(identity), properties);
    }

    /// <summary>
    /// Finishes a sign-in (login page, code step, enrollment alike): the same user's live session here is kept and
    /// its cookie re-issued; another user's is ended so it isn't orphaned (a live row with no cookie); otherwise a
    /// new session starts. False when the session could not start — an operational fault; no cookie is set.
    /// </summary>
    public static async Task<bool> EstablishAsync(
        HttpContext httpContext, Guid tenantId, Guid userId, IReadOnlyList<string> amr,
        ISender sender, ITenantLifetimes tenantLifetimes, CancellationToken cancellationToken)
    {
        var cookieLifetime = (await tenantLifetimes.GetAsync(tenantId, cancellationToken)).SessionCookie;
        var existing = await ReadAsync(httpContext, tenantId, sender, cancellationToken);
        if (existing is not null && existing.UserId == userId)
        {
            await SignInAsync(httpContext, tenantId, userId, existing.Sid, existing.AuthTime, cookieLifetime, amr);
            return true;
        }

        if (existing is not null)
            await sender.Send(
                new EndSessionCommand(tenantId, existing.Sid, EndReason.UserLogout, RequireExisting: false),
                cancellationToken);

        var started = await sender.Send(new StartSessionCommand(tenantId, userId, amr), cancellationToken);
        if (started.IsFailed) return false;

        await SignInAsync(httpContext, tenantId, userId, started.Value.Sid, started.Value.AuthTime, cookieLifetime, amr);
        return true;
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
