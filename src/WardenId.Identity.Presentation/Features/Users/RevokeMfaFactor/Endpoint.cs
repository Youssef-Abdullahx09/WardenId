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
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Users)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapDelete(
                IdentityRoutes.Users.MfaFactor,
                async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromRoute] Guid factorId,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var command = new Command(tenantId, userId, factorId, currentUser.UserId);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Users.RevokeMfaFactor)
            .RequireAuthorization()
            .WithTags(EndpointTags.Users)
            .WithSummary("Revoke an MFA factor")
            .WithDescription("Revokes an enrolled MFA factor. When the MFA policy is Required or RequiredForAdmins, the user's last enabled factor can't be revoked (400); WardenId has no roles, so RequiredForAdmins applies to every user.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
