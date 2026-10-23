using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Audit.Application.Features.Events.List;
using WardenId.Audit.Domain;
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
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                [FromQuery] AuditChannel? channel,
                [FromQuery] Guid? tenantId,
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                [FromQuery] string? eventType,
                [FromQuery] string? actor,
                [FromQuery] string? target,
                [FromQuery] AuditResult? outcome,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new Query(pageNumber, pageSize, channel, tenantId, from, to, eventType, actor, target, outcome),
                    cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Events.List)
            .RequireAuthorization()
            .WithTags(EndpointTags.Events)
            .WithSummary("List audit events")
            .WithDescription(
                "Pages through audit events, newest first, filtered by channel, tenant, occurred-at range, " +
                "event type (e.g. 'admin.entity.update'), actor (the scope-qualified 'platform:{type}:{id}' / " +
                "'tenant:{tenantId}:{type}:{id}' form, or just the actor id), target ('{type}:{id}', matching any " +
                "of the event's targets) and outcome. Each event is summarized from its payload (type, actor, " +
                "targets, outcome); events whose payload was removed by retention are summarized from their " +
                "indexed columns, without severity or outcome reason.")
            .Produces<PaginationListResponse<AuditEventListItem>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
