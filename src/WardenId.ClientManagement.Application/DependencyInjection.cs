using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace WardenId.ClientManagement.Application;

public static class DependencyInjection
{
    public static void AddClientManagementApplication(
        this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
    }
}
