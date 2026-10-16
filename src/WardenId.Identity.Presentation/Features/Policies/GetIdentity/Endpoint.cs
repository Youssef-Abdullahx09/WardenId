using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Identity.Application.Features.Policies.GetIdentity;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Identity.Presentation.Features.Policies.GetIdentity;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Policies")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/policies/identity", async (
                [FromRoute] Guid tenantId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Query(tenantId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Policies.GetIdentity)
            .RequireAuthorization()
            .WithTags("Identity - Policies")
            .WithSummary("Get the identity policy")
            .WithDescription(
                "Returns the password, lockout and MFA policy in effect for the tenant. Read-only. These values are global " +
                "today (the IdentityPolicy config section, with code defaults), so every tenant gets the same ones and " +
                "scope is \"global\"; tenantId is accepted for future per-tenant policies and is not checked for existence. " +
                "lockout.lockoutDuration is an ISO 8601 duration (e.g. \"PT15M\").")
            .Produces<IdentityPolicy>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
