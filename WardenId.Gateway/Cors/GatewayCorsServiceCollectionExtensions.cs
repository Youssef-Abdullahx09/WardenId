using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WardenId.Gateway.Cors;

public static class GatewayCorsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="GatewayCorsPolicyProvider"/> in place of ASP.NET Core's default provider. Modules keep
    /// adding named policies with <c>AddCors(options => options.AddPolicy(...))</c>. Needs the Protocol module's
    /// <c>IProtocolCorsPolicyResolver</c> (AddProtocolPresentation).
    /// </summary>
    public static IServiceCollection AddGatewayCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors();
        services.Configure<GatewayCorsOptions>(configuration.GetSection(GatewayCorsOptions.Section));
        services.Replace(ServiceDescriptor.Transient<ICorsPolicyProvider, GatewayCorsPolicyProvider>());
        return services;
    }
}
