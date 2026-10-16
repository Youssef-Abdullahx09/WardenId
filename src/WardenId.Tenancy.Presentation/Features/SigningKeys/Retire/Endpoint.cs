using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;
using WardenId.Tenancy.Application.Features.SigningKeys.Retire;

namespace WardenId.Tenancy.Presentation.Features.SigningKeys.Retire;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Tenants")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost("/api/v{version:apiVersion}/tenants/{tenantId:guid}/signing-keys/{keyId:guid}/retire", async (
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
            .WithTags("Tenancy - Signing Keys")
            .WithSummary("Retire a Tenant's Signing Key")
            .WithDescription("Removes a Retiring key from JWKS; tokens it signed stop verifying. Wait at least the longest token lifetime after the rotation (the key's retiringSince) before retiring. The active key cannot be retired.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
