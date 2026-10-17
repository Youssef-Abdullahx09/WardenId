using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Identity.Presentation.Features.Policies.UpdateIdentity;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Policies")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPut("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/policies/identity", async (
                [FromRoute] Guid tenantId,
                [FromBody] Request request,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToCommand(tenantId, currentUser.UserId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Policies.UpdateIdentity)
            .RequireAuthorization()
            .WithTags("Identity - Policies")
            .WithSummary("Set the tenant's identity policy overrides")
            .WithDescription(
                "Replaces the tenant's overrides of the global identity policy. Every field is optional: a null or missing " +
                "field or section clears that override and the tenant uses the global value. Allowed ranges: " +
                "password.minLength 8-128, password.historyCount 0-24, lockout.maxFailedAttempts 1-100, " +
                "lockout.lockoutDuration an ISO 8601 duration from PT1M to PT24H; mfa.mode one of Off, Optional, Required, " +
                "RequiredForAdmins; terms.url an absolute http or https URL of at most 2048 characters (blank clears it). 400 when a value is out of range or malformed (nothing is saved). Takes effect for new " +
                "sign-ins, sign-ups and password changes within 30 seconds on every instance (at once on this one). " +
                "Existing passwords that don't meet stricter rules keep working; only new passwords must comply. " +
                "selfRegistration.enabled = false makes POST tenants/{tenantId}/auth/register return 403; admins can " +
                "still create users. terms.url is the link shown at the accept-terms sign-in step. tenantId is not checked for existence.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
