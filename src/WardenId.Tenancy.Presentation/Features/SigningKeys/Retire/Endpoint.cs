using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;
using WardenId.Tenancy.Application.Features.SigningKeys.Retire;
using WardenId.Tenancy.Presentation.Utilities;

namespace WardenId.Tenancy.Presentation.Features.SigningKeys.Retire;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Tenants)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost(TenancyRoutes.SigningKeys.Retire, async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid keyId,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Command(tenantId, keyId, currentUser.UserId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.SigningKeys.Retire)
            .RequireAuthorization()
            .WithTags(EndpointTags.SigningKeys)
            .WithSummary("Retire a Tenant's Signing Key")
            .WithDescription("Removes a Retiring key from JWKS; tokens it signed stop verifying. Wait at least the longest token lifetime after the rotation (the key's retiringSince) before retiring. The active key cannot be retired.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
