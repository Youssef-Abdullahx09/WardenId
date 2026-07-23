using Microsoft.Extensions.DependencyInjection;

namespace WardenId.SharedKernel.FileOperation;

public static class DependencyInjection
{
    public static void AddSharedKernelFileOperation(
        this IServiceCollection services)
    {
        services.AddSingleton<IReadFileService, ReadFileService>();
    }
}