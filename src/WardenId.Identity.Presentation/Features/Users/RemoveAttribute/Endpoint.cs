using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Identity.Application.Features.Users.RemoveAttribute;
using WardenId.Identity.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.Identity.Presentation.Features.Users.RemoveAttribute;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Users)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapDelete(
                IdentityRoutes.Users.Attribute, async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromRoute] string key,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var command = new Command(tenantId, userId, key, currentUser.UserId);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Users.RemoveAttribute)
            .RequireAuthorization()
            .WithTags(EndpointTags.Users)
            .WithSummary("Remove a user attribute")
            .WithDescription("Removes the attribute with this key (URL-encoded, matched exactly). 404 when the user has no such attribute.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
