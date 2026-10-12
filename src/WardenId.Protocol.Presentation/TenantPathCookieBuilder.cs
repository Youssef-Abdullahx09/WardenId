using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using WardenId.Protocol.Application;

namespace WardenId.Protocol.Presentation;

/// <summary>
/// Scopes the session cookie to /tenants/{slug} on every write — sign-in and sliding renewal
/// alike — so one tenant's session is never sent to another tenant's pages.
/// </summary>
public sealed class TenantPathCookieBuilder : RequestPathBaseCookieBuilder
{
    public TenantPathCookieBuilder()
    {
        Name = TenantSession.Scheme;
        HttpOnly = true;
        SecurePolicy = CookieSecurePolicy.Always;
        SameSite = SameSiteMode.Lax;
        IsEssential = true;
    }

    public override CookieOptions Build(HttpContext context, DateTimeOffset expiresFrom)
    {
        var options = base.Build(context, expiresFrom);
        if (TenantPath(context.Request.Path) is { } path) options.Path = path;
        return options;
    }

    public static string? TenantPath(PathString requestPath)
    {
        // "/tenants/{slug}/{rest}" -> ["", "tenants", "{slug}", "{rest}"]
        var segments = requestPath.Value?.Split('/', 4) ?? [];
        return segments.Length >= 3 && segments[1] == "tenants" && TenantSlugFormat.IsValid(segments[2])
            ? $"/tenants/{segments[2]}"
            : null;
    }
}
