using Microsoft.Extensions.DependencyInjection;
using WardenId.Protocol.Query.Contracts;

namespace WardenId.Protocol.Query;

public static class DependencyInjection
{
    public static void AddProtocolQuery(
        this IServiceCollection services)
    {
        services.AddScoped<ISessionsQuery, SessionsQuery>();
        services.AddScoped<IPoliciesQuery, PoliciesQuery>();
    }
}
