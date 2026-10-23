using Microsoft.Extensions.DependencyInjection;
using WardenId.ClientManagement.Query.Contracts;

namespace WardenId.ClientManagement.Query;

public static class DependencyInjection
{
    public static void AddClientManagementQuery(
        this IServiceCollection services)
    {
        services.AddScoped<IClientsQuery, ClientsQuery>();
    }
}
