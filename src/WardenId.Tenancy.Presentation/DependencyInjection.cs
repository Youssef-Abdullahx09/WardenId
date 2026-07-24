using Microsoft.Extensions.DependencyInjection;
using WardenId.Tenancy.Application;

namespace WardenId.Tenancy.Presentation;

public static class DependencyInjection
{
    public static void AddTenancyPresentation(
        this IServiceCollection services)
    {
        services.AddTenancyApplication();
    }
}