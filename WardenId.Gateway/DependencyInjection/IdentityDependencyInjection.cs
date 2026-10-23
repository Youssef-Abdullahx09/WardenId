using WardenId.Identity.Infrastructure;
using WardenId.Identity.Presentation;
using WardenId.Identity.Query;

namespace WardenId.Gateway.DependencyInjection;

public static class IdentityDependencyInjection
{
    public static void AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIdentityPresentation(configuration);
        services.AddIdentityInfrastructure(configuration);
        services.AddIdentityQuery();
    }
}
