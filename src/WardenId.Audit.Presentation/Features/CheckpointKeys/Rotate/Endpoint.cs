using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Audit.Application.Features.CheckpointKeys.Rotate;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Audit.Presentation.Features.CheckpointKeys.Rotate;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Audit)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost(EndpointRoutes.CheckpointKeys.Rotate, async (
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Command(), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.CheckpointKeys.Rotate)
            .RequireAuthorization()
            .WithTags(EndpointTags.Verification)
            .WithSummary("Rotate the checkpoint signing key")
            .WithDescription(
                "Creates a new ES256 checkpoint signing key and makes it the one new checkpoints are signed with. " +
                "The old key stays listed (inactive) so checkpoints it signed still verify, and signs a rotation " +
                "statement for the new key (see the key list's rotationAttestation). Recorded as an " +
                "admin.audit.checkpointkey.rotate event in the platform channel. 409 when a concurrent rotation won.")
            .Produces<RotatedCheckpointKey>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
