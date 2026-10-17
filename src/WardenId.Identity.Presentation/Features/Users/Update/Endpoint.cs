using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Identity.Presentation.Features.Users.Update;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Users")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPatch("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/users/{userId:guid}", async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromBody] Request request,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToCommand(tenantId, userId, currentUser.UserId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Users.Update)
            .RequireAuthorization()
            .WithTags("Identity - Users")
            .WithSummary("Change a user's email or username")
            .WithDescription("Changes the email and/or the username; a null or missing field is left unchanged. Same rules as creating a user: 400 if a value is invalid or already in use in the tenant (soft-deleted users included). A new email must be verified again: the user keeps their status but gets the VerifyEmail required action (no sign-in or tokens until done), a verification email goes to the new address, and verification or password reset links sent to the old one stop working. 400 if the user is disabled.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
