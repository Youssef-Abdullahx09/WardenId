using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using WardenId.Protocol.Application.Features.Jwks.Get;
using WardenId.Protocol.Application.Utilities;
using WardenId.Protocol.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;
using JwksQuery = WardenId.Protocol.Application.Features.Jwks.Get.Query;

namespace WardenId.Protocol.Presentation.Features.Jwks.Get;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ProtocolRoutes.UnderTenantIssuer(ProtocolPaths.Endpoints.Jwks), async (
                [FromRoute] string slug,
                [FromServices] ISender sender,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new JwksQuery(slug), cancellationToken);

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
            .WithSummary("Tenant JSON Web Key Set")
            .WithDescription("Public signing keys (RFC 7517) for verifying tokens issued by this tenant.")
            .Produces<JsonWebKeySetDocument>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
