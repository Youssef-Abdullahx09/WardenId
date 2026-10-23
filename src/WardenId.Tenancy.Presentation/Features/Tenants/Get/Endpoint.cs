using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;
using WardenId.Tenancy.Application.Features.Tenants.Get;
using WardenId.Tenancy.Presentation.Utilities;

namespace WardenId.Tenancy.Presentation.Features.Tenants.Get;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Tenants)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet(TenancyRoutes.Tenants.Item, async (
                [FromRoute] Guid tenantId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Query(tenantId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Tenants.Get)
            .RequireAuthorization()
            .WithTags(EndpointTags.Tenants)
            .WithSummary("Get a Tenant")
            .WithDescription("Returns the tenant with its status and signing key metadata (no key material).")
            .Produces<TenantDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
