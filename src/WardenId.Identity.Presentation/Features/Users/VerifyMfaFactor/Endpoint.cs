using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Identity.Application.Features.Users.VerifyMfaFactor;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Identity.Presentation.Features.Users.VerifyMfaFactor;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Users")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost(
                "/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/users/{userId:guid}/mfa-factors/{factorId:guid}/verify",
                async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromRoute] Guid factorId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Command(tenantId, userId, factorId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .RequireAuthorization()
            .WithTags("Identity - Users")
            .WithSummary("Verify an MFA factor")
            .WithDescription("Marks an enrolled MFA factor as verified/enabled. Orchestration only — actual TOTP code validation is not implemented yet.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
