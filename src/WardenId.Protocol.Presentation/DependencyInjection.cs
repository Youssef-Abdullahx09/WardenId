using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardenId.Protocol.Application;

namespace WardenId.Protocol.Presentation;

public static class DependencyInjection
{
    public static void AddProtocolPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProtocolApplication(configuration);
    }
}
