using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Identity.Application.Features.Users.Enable;
using WardenId.Identity.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.Identity.Presentation.Features.Users.Enable;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Users)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost(IdentityRoutes.Users.Enable, async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Command(tenantId, userId, currentUser.UserId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Users.Enable)
            .RequireAuthorization()
            .WithTags(EndpointTags.Users)
            .WithSummary("Enable a user")
            .WithDescription("Re-enables a disabled user: back to Active, or to PendingVerification if the email was never verified. Clears any lockout; keeps pending required actions. 400 if the user is not disabled.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
