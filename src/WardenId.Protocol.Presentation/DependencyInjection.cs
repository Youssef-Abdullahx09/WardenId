using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application;
using WardenId.Protocol.Presentation.Cors;

namespace WardenId.Protocol.Presentation;

public static class DependencyInjection
{
    public static void AddProtocolPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProtocolApplication(configuration);

        // Browser RPs (oidc-client-ts, Angular) fetch discovery/JWKS cross-origin. Explicit policy so
        // tightening the Gateway's default CORS policy never breaks them. Public data, no credentials.
        services.AddCors(options => options.AddPolicy(
            CorsPolicyNames.ProtocolPublicMetadata,
            policy => policy.AllowAnyOrigin().WithMethods("GET").AllowAnyHeader()));
        // Token, revoke and userinfo: only the tenant's client origins (W17b). The host's CORS policy provider asks it.
        services.AddMemoryCache();
        services.AddScoped<IProtocolCorsPolicyResolver, ProtocolCorsPolicyResolver>();

        services.AddRazorPages(options =>
                options.Conventions.AddFolderApplicationModelConvention(
                    "/Tenants", model => model.Filters.Add(new AuthPageHeadersFilter())))
            .AddApplicationPart(typeof(DependencyInjection).Assembly);

        // No default scheme on purpose: only the login page and /authorize read this cookie.
        services.AddAuthentication()
            .AddCookie(TenantSession.Scheme, options =>
            {
                options.Cookie = new TenantPathCookieBuilder();
                options.SlidingExpiration = true;
            });
        // Fallback only: TenantSession.SignInAsync sets each ticket's expiry from the tenant's cookie lifetime.
        services.AddOptions<CookieAuthenticationOptions>(TenantSession.Scheme)
            .Configure<IOptions<ProtocolSessionOptions>>((cookie, session) =>
                cookie.ExpireTimeSpan = session.Value.CookieLifetime);
    }
}
