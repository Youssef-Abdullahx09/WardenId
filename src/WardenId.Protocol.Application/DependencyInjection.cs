using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WardenId.Protocol.Application;

public static class DependencyInjection
{
    public static void AddProtocolApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
        services.Configure<ProtocolTokenOptions>(configuration.GetSection(ConfigurationKeys.ProtocolToken.Section));
        services.Configure<ProtocolSessionOptions>(configuration.GetSection(ConfigurationKeys.ProtocolSession.Section));
    }
}
