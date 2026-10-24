using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Audit.Application.Features.Events.List;
using WardenId.Audit.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Audit.Presentation.Features.Events.List;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Audit)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet(EndpointRoutes.Events.List, async (
                [AsParameters] Request request,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToQuery(), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Events.List)
            .RequireAuthorization()
            .WithTags(EndpointTags.Events)
            .WithSummary("List audit events")
            .WithDescription(
                "Pages through audit events, filtered by channel, tenant, occurred-at range, " +
                "event type (e.g. 'admin.entity.update'), actor (the scope-qualified 'platform:{type}:{id}' / " +
                "'tenant:{tenantId}:{type}:{id}' form, or just the actor id), target ('{type}:{id}', matching any " +
                "of the event's targets) and outcome. Each event is summarized from its payload (type, actor, " +
                "targets, outcome); events whose payload was removed by retention are summarized from their " +
                "indexed columns, without severity or outcome reason. `sortOption` is `OccurredAt` or `Sequence` " +
                "(the event's position in its channel's log, so only meaningful with a `channel` filter; unsealed " +
                "events, which have none yet, come last) and `sortDirection` is `Asc` or `Desc`; the default is " +
                "`OccurredAt` `Desc` (newest first). `pageNumber` defaults to 1 and `pageSize` to 10 (at most 100).")
            .Produces<PaginationListResponse<AuditEventListItem>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
