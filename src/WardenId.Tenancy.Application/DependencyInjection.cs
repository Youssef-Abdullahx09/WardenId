using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace WardenId.Tenancy.Application;

public static class DependencyInjection
{
    public static void AddTenancyApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
    }
}