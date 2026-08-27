using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace WardenId.Audit.Application;

public static class DependencyInjection
{
    public static void AddAuditApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
    }
}
