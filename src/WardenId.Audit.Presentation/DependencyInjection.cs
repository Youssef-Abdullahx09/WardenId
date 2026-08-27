using Microsoft.Extensions.DependencyInjection;
using WardenId.Audit.Application;

namespace WardenId.Audit.Presentation;

public static class DependencyInjection
{
    public static void AddAuditPresentation(this IServiceCollection services)
    {
        services.AddAuditApplication();
    }
}
