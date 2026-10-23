using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Application.Features.Sessions.List;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Protocol.Presentation.Features.Sessions.List;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Sessions)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet(ProtocolRoutes.Admin.Sessions, async (
                [FromRoute] Guid tenantId,
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                [FromQuery] Guid? userId,
                [FromQuery] string? clientId,
                [FromQuery] bool? includeEnded,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var request = new Request(pageNumber, pageSize, userId, clientId, includeEnded);
                var result = await sender.Send(request.ToQuery(tenantId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Sessions.List)
            .RequireAuthorization()
            .WithTags(EndpointTags.Sessions)
            .WithSummary("List Sessions")
            .WithDescription(
                "Pages through the tenant's login sessions, newest sign-in first, optionally filtered by user and by a client " +
                "(public client_id) that took part. Live sessions only unless includeEnded=true, which adds ended and expired ones " +
                "with their end time and reason. Ended sessions are pruned after ProtocolSession:PruneMinimumAge.")
            .Produces<PaginationListResponse<SessionListItem>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
