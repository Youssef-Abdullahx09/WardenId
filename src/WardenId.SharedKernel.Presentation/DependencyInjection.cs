using Microsoft.Extensions.DependencyInjection;
using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.SharedKernel.Presentation;

public static class DependencyInjection
{
    public static void AddSharedKernelPresentation(
        this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddSingleton<ITenantContext, HttpTenantContext>();
    }
}
