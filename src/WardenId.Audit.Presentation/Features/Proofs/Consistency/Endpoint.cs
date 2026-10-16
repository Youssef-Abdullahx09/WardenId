using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Audit.Application.Features.Proofs.Consistency;
using WardenId.Audit.Domain;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Audit.Presentation.Features.Proofs.Consistency;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Audit")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet("/api/v{version:apiVersion}/audit/channels/{channel}/consistency-proof", async (
                [FromRoute] AuditChannel channel,
                [FromQuery] Guid? tenantId,
                [FromQuery] long earlierSize,
                [FromQuery] long laterSize,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Query(channel, tenantId, earlierSize, laterSize), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Proofs.Consistency)
            .RequireAuthorization()
            .WithTags("Audit - Proofs")
            .WithSummary("Get a consistency proof between two checkpoints")
            .WithDescription(
                "Proves the checkpoint at earlierSize is a strict prefix of the checkpoint at laterSize, for the given channel/tenant scope.")
            .Produces<ConsistencyProofResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
