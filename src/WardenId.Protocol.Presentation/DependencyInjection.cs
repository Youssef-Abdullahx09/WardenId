using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    }
}
