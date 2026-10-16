using Microsoft.AspNetCore.Authorization;
using WardenId.Protocol.Infrastructure.Authentication;

namespace WardenId.Gateway.Authorization;

public static class AdminApiAuthorization
{
    /// <summary>
    /// WardenId access tokens authenticate REST callers (default scheme, so audit sees the caller),
    /// and <c>.RequireAuthorization()</c> means "platform admin". Protocol endpoints and login pages
    /// name their own schemes and are unaffected.
    /// </summary>
    public static void AddAdminApiAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = AccessTokenAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = AccessTokenAuthenticationDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = AccessTokenAuthenticationDefaults.AuthenticationScheme;
        });

        services.Configure<PlatformAdminOptions>(configuration.GetSection(GatewayConfigurationKeys.AdminApi.Section));
        services.AddSingleton<PlatformAdmins>();
        services.AddSingleton<IAuthorizationHandler, PlatformAdminHandler>();

        services.AddAuthorization(options => options.DefaultPolicy =
            new AuthorizationPolicyBuilder(AccessTokenAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new PlatformAdminRequirement())
                .Build());
    }
}
