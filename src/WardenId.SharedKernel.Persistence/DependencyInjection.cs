using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace WardenId.SharedKernel.Persistence;

public static class DependencyInjection
{
    public static void AddSharedKernelPersistence(
        this IServiceCollection services)
    {
        services.AddScoped<ISaveChangesInterceptor, DomainEventsInterceptor>();
    }
}