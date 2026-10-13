using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WardenId.Protocol.Application.Features.Token;

namespace WardenId.Protocol.Application;

public static class DependencyInjection
{
    public static void AddProtocolApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
        services.Configure<ProtocolTokenOptions>(configuration.GetSection(ConfigurationKeys.ProtocolToken.Section));
        services.Configure<ProtocolSessionOptions>(configuration.GetSection(ConfigurationKeys.ProtocolSession.Section));
        // TryAdd: tests register a FakeTimeProvider first; OpenIddict resolves the same TimeProvider.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<UserTokenGrantFactory>();
    }
}
