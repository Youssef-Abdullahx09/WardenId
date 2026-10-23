using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Presentation.Cors;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Gateway.Cors;

/// <summary>
/// Replaces the old any-origin default policy (W17b). Named policies (endpoint <c>.RequireCors(name)</c>, e.g. the
/// any-origin discovery/JWKS policy) resolve as usual. Without one:
/// <list type="bullet">
/// <item>the tenant's browser-facing protocol endpoints allow its clients' origins (<see cref="IProtocolCorsPolicyResolver"/>);</item>
/// <item><c>/api/**</c> allows <see cref="GatewayCorsOptions.AllowedAdminOrigins"/>, none by default;</item>
/// <item>anything else (login pages, authorize, end-session, introspection, health, Swagger) gets no CORS headers.</item>
/// </list>
/// No policy ever allows credentials: protocol and admin calls carry bearer tokens or client credentials, not cookies.
/// </summary>
public sealed class GatewayCorsPolicyProvider(
    IOptions<CorsOptions> corsOptions,
    IOptions<GatewayCorsOptions> gatewayOptions,
    IProtocolCorsPolicyResolver protocol) : ICorsPolicyProvider
{
    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        if (policyName is not null)
            return corsOptions.Value.GetPolicy(policyName);

        if (await protocol.ResolveAsync(context) is { } protocolPolicy)
            return protocolPolicy;

        var adminOrigins = gatewayOptions.Value.AllowedAdminOrigins;
        if (adminOrigins.Length > 0 && context.Request.Path.StartsWithSegments(ApiRoutes.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return new CorsPolicyBuilder()
                .WithOrigins(adminOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .Build();
        }

        return null;
    }
}
