using Microsoft.Extensions.DependencyInjection;
using WardenId.Identity.Query.Contracts;

namespace WardenId.Identity.Query;

public static class DependencyInjection
{
    public static void AddIdentityQuery(
        this IServiceCollection services)
    {
        services.AddScoped<IUsersQuery, UsersQuery>();
        services.AddScoped<IPoliciesQuery, PoliciesQuery>();
    }
}
