using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application;

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
        services.AddOptions<CookieAuthenticationOptions>(TenantSession.Scheme)
            .Configure<IOptions<ProtocolSessionOptions>>((cookie, session) =>
                cookie.ExpireTimeSpan = session.Value.CookieLifetime);
    }
}
