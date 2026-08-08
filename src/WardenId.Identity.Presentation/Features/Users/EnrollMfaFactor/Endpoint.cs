using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Identity.Presentation.Features.Users.EnrollMfaFactor;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Users")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost(
                "/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/users/{userId:guid}/mfa-factors", async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromBody] Request request,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToCommand(tenantId, userId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .RequireAuthorization()
            .WithTags("Identity - Users")
            .WithSummary("Enroll an MFA factor")
            .WithDescription("Enrolls an MFA factor and issues a batch of recovery codes. The raw recovery codes are only ever returned here, once. TOTP secret generation/verification is not implemented yet — CredentialData is accepted as an opaque pre-built value.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
