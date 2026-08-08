using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardenId.Identity.Application;

namespace WardenId.Identity.Presentation;

public static class DependencyInjection
{
    public static void AddIdentityPresentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIdentityApplication(configuration);
    }
}
