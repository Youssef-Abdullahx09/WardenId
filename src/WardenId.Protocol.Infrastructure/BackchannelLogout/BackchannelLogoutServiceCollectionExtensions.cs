using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Infrastructure.Sessions;
using WardenId.Protocol.Infrastructure.Utilities;

namespace WardenId.Protocol.Infrastructure.BackchannelLogout;

public static class BackchannelLogoutServiceCollectionExtensions
{
    /// <summary>
    /// Back-channel logout (spec D8–D11): token factory, HTTP sender, revoker and CAP queue. The CAP subscribers
    /// live in Protocol.Application and are registered by AddProtocolApplication. Kept separate from
    /// AddProtocolInfrastructure, which needs Postgres, so tests can build this exact registration.
    /// </summary>
    public static IServiceCollection AddProtocolBackchannelLogout(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProtocolBackchannelLogoutOptions>()
            .Bind(configuration.GetSection(ConfigurationKeys.ProtocolBackchannelLogout.Section))
            .Validate(options => options.Timeout > TimeSpan.Zero, "ProtocolBackchannelLogout:Timeout must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpClient<IBackchannelLogoutSender, HttpBackchannelLogoutSender>((provider, client) =>
                client.Timeout = provider.GetRequiredService<IOptions<ProtocolBackchannelLogoutOptions>>().Value.Timeout)
            // Server-to-server POST to an RP-registered URI: never follow a redirect somewhere unregistered.
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        services.AddScoped<ILogoutTokenFactory, LogoutTokenFactory>();
        services.AddScoped<ISessionAuthorizationRevoker, OpenIddictSessionAuthorizationRevoker>();
        services.AddScoped<IBackchannelLogoutQueue, CapBackchannelLogoutQueue>();

        return services;
    }
}
