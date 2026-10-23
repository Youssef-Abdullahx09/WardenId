using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Identity.Application.Features.Users.RequiredActions.Clear;
using WardenId.Identity.Domain;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Identity.Presentation.Features.Users.RequiredActions.Clear;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Users)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapDelete(
                IdentityRoutes.Users.RequiredAction,
                async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromRoute] RequiredAction action,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var command = new Command(tenantId, userId, action, currentUser.UserId);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Users.ClearRequiredActions)
            .RequireAuthorization()
            .WithTags(EndpointTags.Users)
            .WithSummary("Clear a required action")
            .WithDescription("Removes a pending required action from the user.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
