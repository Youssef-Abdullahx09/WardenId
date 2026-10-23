using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Audit.Application.Features.Events.Get;
using WardenId.Audit.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.Audit.Presentation.Features.Events.Get;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Audit)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet(EndpointRoutes.Events.Get, async (
                [FromRoute] Guid eventId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Query(eventId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Events.Get)
            .RequireAuthorization()
            .WithTags(EndpointTags.Events)
            .WithSummary("Get an audit event")
            .WithDescription(
                "Returns one audit event with its decoded payload: actor, targets, outcome, client, authentication " +
                "context, correlation and property changes (redacted values stay redacted: hash and prefix only). " +
                "When retention has removed the payload, returns the indexed summary with payloadAvailable false " +
                "and no envelope.")
            .Produces<AuditEventDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
