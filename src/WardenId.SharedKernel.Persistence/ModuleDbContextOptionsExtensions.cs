using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace WardenId.SharedKernel.Persistence;

public static class ModuleDbContextOptionsExtensions
{
    /// <summary>
    /// Wires every ISaveChangesInterceptor registered anywhere in DI (DomainEventsInterceptor,
    /// Audit's AuditCaptureInterceptor, any future cross-cutting interceptor) into a module's
    /// DbContext without that module needing a reference to — or knowledge of — the interceptor's
    /// owning module. Add a new cross-cutting interceptor once, as ISaveChangesInterceptor, and
    /// every module context picks it up automatically.
    /// </summary>
    public static void AddSharedInterceptors(this DbContextOptionsBuilder options, IServiceProvider serviceProvider) =>
        options.AddInterceptors(serviceProvider.GetServices<ISaveChangesInterceptor>());
}
