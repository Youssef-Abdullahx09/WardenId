using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Application.Features.Discovery.Get;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Protocol.Presentation.Features.Discovery.Get;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ProtocolRoutes.TenantIssuer + "/.well-known/openid-configuration", async (
                [FromRoute] string slug,
                [FromServices] ISender sender,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Query(slug), cancellationToken);

                if (result.IsFailed)
                {
                    httpContext.Response.Headers.CacheControl = "no-store";
                    return result.HandleApiFailure();
                }

                httpContext.Response.Headers.CacheControl = "public, max-age=300";
                return Results.Ok(result.Value);
            })
            .AllowAnonymous()
            .RequireCors(CorsPolicyNames.ProtocolPublicMetadata)
            .WithTags("Protocol - Discovery")
            .WithSummary("OpenID Connect discovery document")
            .WithDescription("Per-tenant OIDC Discovery 1.0 metadata. Unversioned; issuer = {base}/tenants/{slug}.")
            .Produces<DiscoveryDocument>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
