using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Application.Features.Jwks.Get;
using JwksQuery = WardenId.Protocol.Application.Features.Jwks.Get.Query;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Protocol.Presentation.Features.Jwks.Get;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ProtocolRoutes.TenantIssuer + "/jwks", async (
                [FromRoute] string slug,
                [FromServices] ISender sender,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new JwksQuery(slug), cancellationToken);

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
            .WithSummary("Tenant JSON Web Key Set")
            .WithDescription("Public signing keys (RFC 7517) for verifying tokens issued by this tenant.")
            .Produces<JsonWebKeySetDocument>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
