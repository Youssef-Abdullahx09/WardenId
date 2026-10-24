using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;
using WardenId.SharedKernel.Query.Contracts;
using WardenId.Tenancy.Presentation.Utilities;
using WardenId.Tenancy.Query.Contracts.Features.Tenants.List;

namespace WardenId.Tenancy.Presentation.Features.Tenants.List;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Tenants)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet(TenancyRoutes.Tenants.Collection, async (
                [AsParameters] Request request,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToQuery(), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Tenants.List)
            .RequireAuthorization()
            .WithTags(EndpointTags.Tenants)
            .WithSummary("List Tenants")
            .WithDescription(
                "Lists tenants, paginated and optionally filtered by `status`. `sortOption` is `CreatedAt`, `Slug` " +
                "or `DisplayName` and `sortDirection` is `Asc` or `Desc`; the default is `CreatedAt` `Desc` " +
                "(newest first). `pageNumber` defaults to 1 and `pageSize` to 10.")
            .Produces<PaginationListResponse<Item>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
