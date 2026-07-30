using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WardenId.Identity.Application;

public static class DependencyInjection
{
    public static void AddIdentityApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
        services.Configure<IdentityPolicyOptions>(configuration.GetSection(ConfigurationKeys.IdentityPolicy.Section));
    }
}
