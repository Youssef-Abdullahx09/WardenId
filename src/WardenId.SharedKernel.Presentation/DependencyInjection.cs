using Microsoft.Extensions.DependencyInjection;

namespace WardenId.SharedKernel.Presentation;

public static class DependencyInjection
{
    public static void AddSharedKernelPresentation(
        this IServiceCollection services)
    {
        services.AddScoped<ICurrentUser, CurrentUser>();
    }
}