using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;
using WardenId.Tenancy.Application.Features.Tenants.Get;
using Query = WardenId.Tenancy.Application.Features.Tenants.GetBySlug.Query;

namespace WardenId.Tenancy.Presentation.Features.Tenants.GetBySlug;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Tenants")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet("/api/v{version:apiVersion}/tenants/by-slug/{slug}", async (
                [FromRoute] string slug,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Query(slug), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Tenants.GetBySlug)
            .RequireAuthorization()
            .WithTags("Tenancy - Tenants")
            .WithSummary("Get a Tenant by slug")
            .WithDescription("Same as Get a Tenant, looked up by its slug (the issuer path segment).")
            .Produces<TenantDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
