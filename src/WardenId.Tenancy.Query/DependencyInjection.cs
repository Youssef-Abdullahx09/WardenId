using Microsoft.Extensions.DependencyInjection;
using WardenId.Tenancy.Query.Contracts;

namespace WardenId.Tenancy.Query;

public static class DependencyInjection
{
    public static void AddTenancyQuery(
        this IServiceCollection services)
    {
        services.AddScoped<ITenantsQuery, TenantsQuery>();
        services.AddScoped<ISigningKeysQuery, SigningKeysQuery>();
    }
}
