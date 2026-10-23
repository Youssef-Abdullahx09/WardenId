using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using WardenId.Protocol.Application.Features.Discovery.Get;
using WardenId.Protocol.Application.Utilities;
using WardenId.Protocol.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;
using DiscoveryQuery = WardenId.Protocol.Application.Features.Discovery.Get.Query;

namespace WardenId.Protocol.Presentation.Features.Discovery.Get;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ProtocolRoutes.UnderTenantIssuer(ProtocolPaths.Endpoints.Discovery), async (
                [FromRoute] string slug,
                [FromServices] ISender sender,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new DiscoveryQuery(slug), cancellationToken);

                if (result.IsFailed)
                {
                    httpContext.Response.Headers.CacheControl = CacheControlHeaderValue.NoStoreString;
                    return result.HandleApiFailure();
                }

                httpContext.Response.Headers.CacheControl = HeaderValues.PublicMetadataCacheControl;
                return Results.Ok(result.Value);
            })
            .AllowAnonymous()
            .RequireCors(CorsPolicyNames.ProtocolPublicMetadata)
            .WithTags(EndpointTags.Discovery)
            .WithSummary("OpenID Connect discovery document")
            .WithDescription("Per-tenant OIDC Discovery 1.0 metadata. Unversioned; issuer = {base}/tenants/{slug}.")
            .Produces<DiscoveryDocument>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
