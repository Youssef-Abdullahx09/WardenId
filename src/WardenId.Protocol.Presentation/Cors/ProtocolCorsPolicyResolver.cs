using MediatR;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using WardenId.Protocol.Application;
using ListAllowedOriginsQuery = WardenId.Protocol.Application.Features.Cors.ListAllowedOrigins.Query;

namespace WardenId.Protocol.Presentation.Cors;

/// <summary>
/// <c>/tenants/{slug}/token</c>, <c>/revoke</c> and <c>/userinfo</c> are what a browser SPA (oidc-client-ts and the like)
/// calls cross-origin. They allow the union of the CORS origins configured on the tenant's enabled clients: a preflight
/// carries no client_id, so the requesting client can't be known before the browser decides, and the tenant is the
/// narrowest scope available. Any client of the tenant thus lends its origins to the others; the token endpoint still
/// authenticates the client and checks redirect URIs and PKCE, so an allowed origin gains no more than being able to
/// read the responses.
/// <para>
/// Not covered (null): introspection (needs client authentication; public clients are refused), authorize and
/// end-session (top-level navigations, no CORS), and discovery/JWKS, which keep their own any-origin policy
/// (<see cref="CorsPolicyNames.ProtocolPublicMetadata"/>).
/// </para>
/// <para>
/// Origins are cached per slug for <see cref="CacheDuration"/>, so a change on a client takes up to that long to apply.
/// </para>
/// </summary>
public sealed class ProtocolCorsPolicyResolver(ISender sender, IMemoryCache cache) : IProtocolCorsPolicyResolver
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    private static readonly HashSet<string> BrowserFacingEndpoints = new(StringComparer.Ordinal)
    {
        ProtocolPaths.Endpoints.Token, ProtocolPaths.Endpoints.Revoke, ProtocolPaths.Endpoints.UserInfo
    };

    public async Task<CorsPolicy?> ResolveAsync(HttpContext context)
    {
        // "/tenants/{slug}/{endpoint}" -> ["", "tenants", "{slug}", "{endpoint}"]
        var segments = context.Request.Path.Value?.Split('/') ?? [];
        if (segments.Length != 4 || segments[1] != ProtocolPaths.TenantsSegment || !BrowserFacingEndpoints.Contains(segments[3]))
            return null;

        var slug = segments[2];
        var origins = await cache.GetOrCreateAsync(
            ProtocolCacheKeys.CorsOriginsPrefix + slug,
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                entry.Size = 1;
                return sender.Send(new ListAllowedOriginsQuery(slug), context.RequestAborted);
            }) ?? [];

        // An empty origin list is a policy that allows nothing: the browser gets no CORS headers.
        return new CorsPolicyBuilder()
            .WithOrigins([.. origins])
            .WithMethods(HttpMethods.Get, HttpMethods.Post)
            .AllowAnyHeader()
            .SetPreflightMaxAge(CacheDuration)
            .Build();
    }
}
