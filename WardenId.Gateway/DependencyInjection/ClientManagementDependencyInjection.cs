using WardenId.ClientManagement.Infrastructure;
using WardenId.ClientManagement.Presentation;
using WardenId.ClientManagement.Query;

namespace WardenId.Gateway.DependencyInjection;

public static class ClientManagementDependencyInjection
{
    public static void AddClientManagementModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddClientManagementPresentation();
        services.AddClientManagementInfrastructure(configuration);
        services.AddClientManagementQuery();
    }
}
