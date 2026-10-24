using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Presentation.Utilities;
using WardenId.Protocol.Query.Contracts.Features.Sessions.List;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;
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
                [AsParameters] Request request,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
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
                "Pages through the tenant's login sessions, optionally filtered by `userId` and by a `clientId` (public " +
                "client_id) that took part; a blank `clientId` filters nothing. Live sessions only unless " +
                "`includeEnded=true` (default false), which adds ended and expired ones with their end time and reason. " +
                "`sortOption` is `AuthTime` or `ExpiresAt` and `sortDirection` is `Asc` or `Desc`; the default is " +
                "`AuthTime` `Desc` (newest sign-in first). `pageNumber` defaults to 1 and `pageSize` to 10 (at most 100). " +
                "Ended sessions are pruned after ProtocolSession:PruneMinimumAge.")
            .Produces<PaginationListResponse<Item>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
