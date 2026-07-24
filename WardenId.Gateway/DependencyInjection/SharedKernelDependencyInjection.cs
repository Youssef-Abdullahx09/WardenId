using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Infrastructure;
using WardenId.SharedKernel.Persistence;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Query;

namespace WardenId.Gateway.DependencyInjection;

public static class SharedKernelDependencyInjection
{
    public static void AddSharedKernelModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        
        services.AddSharedKernelPresentation();
        services.AddSharedKernelPersistence();
        services.AddSharedKernelFileOperation();
        services.AddSharedKernelQuery(configuration);
        services.AddSharedKernelInfrastructure(configuration);
    }
}