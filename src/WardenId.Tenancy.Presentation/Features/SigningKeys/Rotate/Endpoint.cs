using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;
using WardenId.Tenancy.Application.Features.SigningKeys.Rotate;
using WardenId.Tenancy.Presentation.Utilities;

namespace WardenId.Tenancy.Presentation.Features.SigningKeys.Rotate;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Tenants)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost(TenancyRoutes.SigningKeys.Rotate, async (
                [FromRoute] Guid tenantId,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Command(tenantId, currentUser.UserId), cancellationToken);

                return result.IsSuccess ? Results.Ok(new Response(result.Value)) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.SigningKeys.Rotate)
            .RequireAuthorization()
            .WithTags(EndpointTags.SigningKeys)
            .WithSummary("Rotate a Tenant's Signing Key")
            .WithDescription("Generates a new ES256 key that signs from now on. The previous key becomes Retiring: it stops signing but stays in JWKS so tokens it signed still verify until it is retired.")
            .Produces<Response>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
