using Microsoft.Extensions.DependencyInjection;
using WardenId.ClientManagement.Application;

namespace WardenId.ClientManagement.Presentation;

public static class DependencyInjection
{
    public static void AddClientManagementPresentation(
        this IServiceCollection services)
    {
        services.AddClientManagementApplication();
    }
}
