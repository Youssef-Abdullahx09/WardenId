using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Identity.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.Identity.Presentation.Features.Users.Register;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Users)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost(IdentityRoutes.UserAuth.Register, async (
                [FromRoute] Guid tenantId,
                [FromBody] Request request,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToCommand(tenantId), cancellationToken);

                return result.IsSuccess
                    ? Results.Created(
                        IdentityRoutes.Users.Location(tenantId, result.Value.UserId),
                        new Response(result.Value.UserId))
                    : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Users.Register)
            .WithTags(EndpointTags.Users)
            .WithSummary("Register a user")
            .WithDescription("Self-registers a new user for the given tenant. An email-verification code is sent to the given address. " +
                "403 when the tenant's identity policy disables self-registration (selfRegistration.enabled), checked before anything else.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
