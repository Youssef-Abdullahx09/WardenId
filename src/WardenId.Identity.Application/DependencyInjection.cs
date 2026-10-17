using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application;

public static class DependencyInjection
{
    public static void AddIdentityApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
        services.Configure<IdentityPolicyOptions>(configuration.GetSection(ConfigurationKeys.IdentityPolicy.Section));
        services.AddScoped<IIdentityEmailSender, IdentityEmailSender>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IdentityTenantPolicyCache>();
        services.AddScoped<IdentityPolicyResolver>();
    }
}
