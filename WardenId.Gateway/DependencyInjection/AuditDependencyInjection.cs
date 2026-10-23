using WardenId.Audit.Infrastructure;
using WardenId.Audit.Presentation;
using WardenId.Audit.Query;

namespace WardenId.Gateway.DependencyInjection;

public static class AuditDependencyInjection
{
    // AuditContextMiddleware is still wired directly into Program.cs's pipeline, not here —
    // no module currently modularizes app.Use(...) registration.
    public static void AddAuditModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuditPresentation();
        services.AddAuditInfrastructure(configuration);
        services.AddAuditQuery();
    }
}
