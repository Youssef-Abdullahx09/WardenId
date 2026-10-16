using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Audit.Application.Features.Events.List;
using WardenId.Audit.Domain;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Audit.Presentation.Features.Events.List;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Audit")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet("/api/v{version:apiVersion}/audit/events", async (
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                [FromQuery] AuditChannel? channel,
                [FromQuery] Guid? tenantId,
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new Query(pageNumber, pageSize, channel, tenantId, from, to), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Events.List)
            .RequireAuthorization()
            .WithTags("Audit - Events")
            .WithSummary("List audit events")
            .WithDescription(
                "Pages through audit events, newest first, filtered by channel, tenant and occurred-at range. " +
                "Each event is summarized from its payload (type, actor, targets, outcome); events whose payload " +
                "was removed by retention keep only the table columns.")
            .Produces<PaginationListResponse<AuditEventListItem>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
