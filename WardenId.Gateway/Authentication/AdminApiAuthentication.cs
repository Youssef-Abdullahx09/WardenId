using WardenId.Protocol.Infrastructure.Authentication;

namespace WardenId.Gateway.Authentication;

public static class AdminApiAuthentication
{
    /// <summary>
    /// WardenId access tokens authenticate REST callers: default scheme, so audit sees the caller and
    /// <c>.RequireAuthorization()</c> on an endpoint means "any valid access token". Protocol endpoints
    /// and login pages name their own schemes and are unaffected.
    /// </summary>
    public static void AddAdminApiAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = AccessTokenAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = AccessTokenAuthenticationDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = AccessTokenAuthenticationDefaults.AuthenticationScheme;
        });
        services.AddAuthorization();
    }
}
