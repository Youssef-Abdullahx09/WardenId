using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using WardenId.SharedKernel.Application;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Presentation;

public sealed record TenantSessionInfo(Guid UserId, DateTimeOffset AuthTime);

/// <summary>The per-tenant login session (cookie scheme) shared by the login page and /authorize.</summary>
public static class TenantSession
{
    public const string Scheme = "WardenId.TenantSession";

    public static Task SignInAsync(HttpContext httpContext, Guid tenantId, Guid userId)
    {
        var identity = new ClaimsIdentity(Scheme, Claims.Subject, Claims.Role);
        identity.AddClaim(new Claim(Claims.Subject, userId.ToString()));
        identity.AddClaim(new Claim(CustomClaimTypes.TenantId, tenantId.ToString()));
        identity.AddClaim(new Claim(
            Claims.AuthenticationTime,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64));
        identity.AddClaim(new Claim(Claims.AuthenticationMethodReference, "pwd"));

        return httpContext.SignInAsync(Scheme, new ClaimsPrincipal(identity));
    }

    /// <summary>The session for <paramref name="tenantId"/>, or null — a ticket for another tenant counts as none.</summary>
    public static async Task<TenantSessionInfo?> ReadAsync(HttpContext httpContext, Guid tenantId)
    {
        var result = await httpContext.AuthenticateAsync(Scheme);
        if (!result.Succeeded) return null;

        var principal = result.Principal;
        if (principal.FindFirst(CustomClaimTypes.TenantId)?.Value != tenantId.ToString()) return null;
        if (!Guid.TryParse(principal.FindFirst(Claims.Subject)?.Value, out var userId)) return null;
        if (!long.TryParse(principal.FindFirst(Claims.AuthenticationTime)?.Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var authTime)) return null;

        return new TenantSessionInfo(userId, DateTimeOffset.FromUnixTimeSeconds(authTime));
    }
}
