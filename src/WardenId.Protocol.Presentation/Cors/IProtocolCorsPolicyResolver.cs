using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace WardenId.Protocol.Presentation.Cors;

/// <summary>
/// The CORS policy of a tenant's browser-facing protocol endpoints. The Gateway's CORS policy provider asks this first.
/// </summary>
public interface IProtocolCorsPolicyResolver
{
    /// <summary>Null when the request isn't for a browser-facing protocol endpoint (the caller decides then).</summary>
    Task<CorsPolicy?> ResolveAsync(HttpContext context);
}
