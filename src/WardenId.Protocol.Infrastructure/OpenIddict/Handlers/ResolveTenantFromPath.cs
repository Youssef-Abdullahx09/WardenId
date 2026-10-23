using Microsoft.AspNetCore;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using WardenId.Protocol.Application;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.Domain;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// Runs right after OpenIddict resolves the request URI and before it infers the endpoint:
/// for /tenants/{slug}/... sets BaseUri to the tenant issuer, so the issuer and every relative
/// endpoint URI ("token") become per-tenant. Any other path gets BaseUri = null, so no OpenIddict
/// endpoint can match outside a tenant (e.g. bare /token). No I/O here — eligibility is checked
/// by ValidateTenantForIssuance only once an endpoint actually matched.
/// </summary>
public sealed class ResolveTenantFromPath(IOptions<ProtocolTokenOptions> options, ITenantContext tenantContext)
    : IOpenIddictServerHandler<ProcessRequestContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessRequestContext>()
            .UseSingletonHandler<ResolveTenantFromPath>()
            .SetOrder(OpenIddictServerAspNetCoreHandlers.ResolveRequestUri.Descriptor.Order + 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(ProcessRequestContext context)
    {
        var request = context.Transaction.GetHttpRequest()
            ?? throw new InvalidOperationException("The ASP.NET Core request cannot be resolved.");

        // "/tenants/{slug}/{rest}" -> ["", "tenants", "{slug}", "{rest}"]
        var segments = request.Path.Value?.Split('/', 4) ?? [];

        if (segments.Length == 4 && segments[1] == ProtocolPaths.TenantsSegment && TenantSlugFormat.IsValid(segments[2]))
        {
            var slug = segments[2];
            var issuerBase = options.Value.IssuerBaseUri;

            context.BaseUri = new Uri(IssuerUri.For(issuerBase, slug), UriKind.Absolute);
            // Canonicalize onto the configured public base instead of trusting the Host header.
            context.RequestUri = new Uri(
                issuerBase.TrimEnd('/') + request.Path + request.QueryString, UriKind.Absolute);
            tenantContext.SetSlug(slug);
        }
        else
        {
            context.BaseUri = null;
        }

        return ValueTask.CompletedTask;
    }
}
