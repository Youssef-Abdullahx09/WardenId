using MediatR;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using OpenIddict.Server;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using ResolveForIssuanceQuery = WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance.Query;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Handlers;

/// <summary>
/// After the endpoint is inferred: an OpenIddict endpoint was hit under /tenants/{slug}/.
/// Unknown / non-servable tenant -> bare 404 (same as discovery). Suspended -> unauthorized_client
/// (suspension is enforced at issuance). Otherwise records the tenant id for stores and the endpoint.
/// </summary>
public sealed class ValidateTenantForIssuance(ISender sender, ITenantContext tenantContext) : IOpenIddictServerHandler<ProcessRequestContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessRequestContext>()
            .UseScopedHandler<ValidateTenantForIssuance>()
            .SetOrder(OpenIddictServerHandlers.InferEndpointType.Descriptor.Order + 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ProcessRequestContext context)
    {
        if (context.EndpointType is OpenIddictServerEndpointType.Unknown) return;

        var httpContext = context.Transaction.GetHttpRequest()?.HttpContext
            ?? throw new InvalidOperationException("The ASP.NET Core request cannot be resolved.");

        if (tenantContext.Slug is not { } slug)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            context.HandleRequest();
            return;
        }

        var result = await sender.Send(new ResolveForIssuanceQuery(slug), httpContext.RequestAborted);
        if (result.IsFailed)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            context.HandleRequest();
            return;
        }

        if (result.Value.Status == TenantIssuanceStatus.Suspended)
        {
            context.Reject(error: Errors.UnauthorizedClient, description: "The tenant is suspended.");
            return;
        }

        tenantContext.SetTenantId(result.Value.TenantId);
    }
}
