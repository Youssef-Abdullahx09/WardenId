using WardenId.Identity.Infrastructure;
using WardenId.Identity.Presentation;

namespace WardenId.Gateway.DependencyInjection;

public static class IdentityDependencyInjection
{
    public static void AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIdentityPresentation(configuration);
        services.AddIdentityInfrastructure(configuration);
    }
}
