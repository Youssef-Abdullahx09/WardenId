using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Audit.Application.Features.CheckpointKeys.List;
using WardenId.Audit.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.Audit.Presentation.Features.CheckpointKeys.List;

// Aliased inside the namespace: WardenId.Audit.Query (the Dapper read side) would otherwise shadow the request type.
using Query = WardenId.Audit.Application.Features.CheckpointKeys.List.Query;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Audit)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet(EndpointRoutes.CheckpointKeys.List, async (
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Query(), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.CheckpointKeys.List)
            .RequireAuthorization()
            .WithTags(EndpointTags.Verification)
            .WithSummary("List checkpoint signing public keys")
            .WithDescription("Publishes every checkpoint signing key's public JWK (spec §6) — needed to verify checkpoint signatures independently.")
            .Produces<IReadOnlyList<CheckpointKeySummary>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
