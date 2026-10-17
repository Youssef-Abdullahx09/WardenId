using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Identity.Application.Features.Users.ResetMfa;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Identity.Presentation.Features.Users.ResetMfa;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Users")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/users/{userId:guid}/mfa/reset", async (
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
            .WithName(EndpointNames.Users.ResetMfa)
            .RequireAuthorization()
            .WithTags("Identity - Users")
            .WithSummary("Reset a user's MFA")
            .WithDescription("Removes all of the user's MFA factors and recovery codes and adds the EnrollMfa required action, so the user sets up a new authenticator at their next sign-in. Works whatever the MFA policy; admins can't enroll or verify factors for a user. Fails (400) for a disabled user.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
