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
using WardenId.Tenancy.Application.Features.Tenants.List;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Presentation.Utilities;
using ListQuery = WardenId.Tenancy.Application.Features.Tenants.List.Query;

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
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                [FromQuery] Status? status,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ListQuery(pageNumber, pageSize, status), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Tenants.List)
            .RequireAuthorization()
            .WithTags(EndpointTags.Tenants)
            .WithSummary("List Tenants")
            .WithDescription("Lists tenants, newest first, paginated and optionally filtered by status.")
            .Produces<PaginationListResponse<TenantListItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
