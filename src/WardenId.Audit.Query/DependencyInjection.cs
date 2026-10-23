using Microsoft.Extensions.DependencyInjection;
using WardenId.Audit.Query.Contracts;

namespace WardenId.Audit.Query;

public static class DependencyInjection
{
    public static void AddAuditQuery(
        this IServiceCollection services)
    {
        services.AddScoped<IAuditEventsQuery, AuditEventsQuery>();
    }
}
