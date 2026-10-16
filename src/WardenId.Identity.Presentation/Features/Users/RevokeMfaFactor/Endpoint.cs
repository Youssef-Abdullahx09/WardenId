using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Identity.Application.Features.Users.RevokeMfaFactor;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Identity.Presentation.Features.Users.RevokeMfaFactor;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Users")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapDelete(
                "/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/users/{userId:guid}/mfa-factors/{factorId:guid}",
                async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromRoute] Guid factorId,
                [FromQuery] bool targetUserIsAdmin,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var command = new Command(tenantId, userId, factorId, currentUser.UserId, targetUserIsAdmin);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Users.RevokeMfaFactor)
            .RequireAuthorization()
            .WithTags("Identity - Users")
            .WithSummary("Revoke an MFA factor")
            .WithDescription("Revokes an enrolled MFA factor. `targetUserIsAdmin` describes the user being revoked (not the caller) — pass true if MFA is required for admins and this is their last enabled factor. No roles system exists yet, so this is caller-supplied.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
