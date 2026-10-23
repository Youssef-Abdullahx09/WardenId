using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Audit.Application.Features.Verification.Verify;
using WardenId.Audit.Domain;
using WardenId.Audit.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.Audit.Presentation.Features.Verification.Verify;

// Aliased inside the namespace: WardenId.Audit.Query (the Dapper read side) would otherwise shadow the request type.
using Query = WardenId.Audit.Application.Features.Verification.Verify.Query;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Audit)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet(EndpointRoutes.Verification.Verify, async (
                [FromRoute] AuditChannel channel,
                [FromQuery] Guid? tenantId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Query(channel, tenantId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Verification.Verify)
            .RequireAuthorization()
            .WithTags(EndpointTags.Verification)
            .WithSummary("Run full chain verification for a channel/tenant scope")
            .WithDescription(
                "Recomputes segment roots, header chain linkage, sequence density, and checkpoint signatures. " +
                "Always returns 200 with a report — a broken chain is Verified=false in the body, not an HTTP error.")
            .Produces<VerificationReport>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
