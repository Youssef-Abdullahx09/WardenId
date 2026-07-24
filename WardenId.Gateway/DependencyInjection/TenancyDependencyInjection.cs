using WardenId.Tenancy.Infrastructure;
using WardenId.Tenancy.Presentation;

namespace WardenId.Gateway.DependencyInjection;

public static class TenancyDependencyInjection
{
    public static void AddTenancyModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTenancyPresentation();
        services.AddTenancyInfrastructure(configuration);
    }
}