using WardenId.Protocol.Infrastructure;
using WardenId.Protocol.Presentation;
using WardenId.Protocol.Query;

namespace WardenId.Gateway.DependencyInjection;

public static class ProtocolDependencyInjection
{
    public static void AddProtocolModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProtocolPresentation(configuration);
        services.AddProtocolInfrastructure(configuration);
        services.AddProtocolQuery();
    }
}
