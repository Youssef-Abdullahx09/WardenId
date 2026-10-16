using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace WardenId.Protocol.Infrastructure.Authentication;

public static class AccessTokenAuthenticationExtensions
{
    /// <summary>
    /// Registers <see cref="AccessTokenAuthenticationDefaults.AuthenticationScheme"/>. Doesn't make it the
    /// default scheme or require it anywhere: the host decides which endpoints use it.
    /// </summary>
    public static AuthenticationBuilder AddAccessTokenAuthentication(this IServiceCollection services) =>
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, AccessTokenAuthenticationHandler>(
                AccessTokenAuthenticationDefaults.AuthenticationScheme, configureOptions: null);
}
