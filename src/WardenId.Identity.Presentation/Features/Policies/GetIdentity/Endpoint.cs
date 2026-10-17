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
                "Returns the password, lockout, MFA, self-registration and terms of service policy in effect for the tenant: the tenant's " +
                "overrides (set with PUT) over the global values (the IdentityPolicy config section, with code defaults). " +
                "overrides lists the fields that come from the tenant, as paths into this response (e.g. \"password.minLength\"); " +
                "scope is \"tenant\" when that list is non-empty, else \"global\". tenantId is not checked for existence. " +
                "lockout.lockoutDuration is an ISO 8601 duration (e.g. \"PT15M\").")
            .Produces<IdentityPolicy>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
