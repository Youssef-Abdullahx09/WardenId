using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.ClientManagement.Presentation.Utilities;
using WardenId.ClientManagement.Query.Contracts.Features.Clients.List;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.ClientManagement.Presentation.Features.Clients.List;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Clients)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet(ClientManagementRoutes.Clients, async (
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
            .WithName(EndpointNames.Clients.List)
            .RequireAuthorization()
            .WithTags(EndpointTags.Clients)
            .WithSummary("List Clients")
            .WithDescription(
                "Lists the given tenant's clients, paginated and optionally filtered by `enabled` (`true` or `false`) " +
                "and `clientType` (`Confidential` or `Public`). `sortOption` is `CreatedAt`, `ClientId` or " +
                "`DisplayName` and `sortDirection` is `Asc` or `Desc`; the default is `CreatedAt` `Desc` (newest " +
                "first). `pageNumber` defaults to 1 and `pageSize` to 10.")
            .Produces<PaginationListResponse<Item>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
